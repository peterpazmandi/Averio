using Averio.Application.Common.Exceptions;
using Averio.Application.PersonalMoney;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Averio.Worker;

public sealed class RecurringAllowanceBatch(
    IServiceScopeFactory scopes, ILogger<RecurringAllowanceBatch> logger, TimeProvider clock)
{
    private RecurringAllowanceCursor? cursor;

    public async Task RunAsync(CancellationToken ct)
    {
        IReadOnlyList<RecurringAllowanceCandidate> candidates;
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        await using (var scope = scopes.CreateAsyncScope())
            candidates = await scope.ServiceProvider.GetRequiredService<IRecurringAllowanceRepository>()
                .DueAsync(today, 100, ct, cursor);
        if (candidates.Count == 0 && cursor is not null)
        {
            cursor = null;
            await using var scope = scopes.CreateAsyncScope();
            candidates = await scope.ServiceProvider.GetRequiredService<IRecurringAllowanceRepository>()
                .DueAsync(today, 100, ct);
        }
        if (candidates.Count > 0) cursor = new(candidates[^1].DueOn, candidates[^1].RuleId);
        foreach (var candidate in candidates)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<RecurringAllowanceHandler>()
                    .ProcessAsync(candidate, ct);
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
        }
    }
}
