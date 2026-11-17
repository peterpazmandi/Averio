using Averio.Application.Common.Exceptions;
using Averio.Application.PersonalMoney;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Averio.Worker;

public sealed class RecurringAllowanceBatch(
    IServiceScopeFactory scopes, ILogger<RecurringAllowanceBatch> logger, TimeProvider clock)
{
    public async Task RunAsync(CancellationToken ct)
    {
        const int pageSize = 100;
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var unsuccessful = new HashSet<Guid>();
        bool advanced;
        do
        {
            advanced = false;
            RecurringAllowanceCursor? cursor = null;
            while (true)
            {
                IReadOnlyList<RecurringAllowanceCandidate> candidates;
                await using (var scope = scopes.CreateAsyncScope())
                    candidates = await scope.ServiceProvider.GetRequiredService<IRecurringAllowanceRepository>()
                        .DueAsync(today, pageSize, ct, cursor);
                if (candidates.Count == 0) break;

                foreach (var candidate in candidates)
                {
                    ct.ThrowIfCancellationRequested();
                    if (unsuccessful.Contains(candidate.RuleId)) continue;

                    if (await RunCandidateAsync(candidate, ct))
                        advanced = true;
                    else
                        unsuccessful.Add(candidate.RuleId);
                }

                if (candidates.Count < pageSize) break;
                cursor = new(candidates[^1].DueOn, candidates[^1].RuleId);
            }
            // A rule may still have overdue occurrences after advancing its due date.
        } while (advanced);
    }

    private async Task<bool> RunCandidateAsync(RecurringAllowanceCandidate candidate, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<RecurringAllowanceHandler>().ProcessAsync(candidate, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is BusinessRuleException or ForbiddenException
            or NotFoundException or ConflictException or OnboardingRequiredException or ArgumentException)
        {
            // Log only the opaque schedule ID; never expose member identity or amounts.
            logger.LogWarning("Recurring allowance rule {RuleId} needs review", candidate.RuleId);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<RecurringAllowanceHandler>()
                    .BlockAsync(candidate, ct);
            }
            catch (NotFoundException) { /* Recipient left. Old membership stays historical. */ }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception failure)
            {
                logger.LogError(failure, "Could not mark recurring allowance rule {RuleId}", candidate.RuleId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Recurring allowance rule {RuleId} failed; will retry", candidate.RuleId);
        }
        return false;
    }
}
