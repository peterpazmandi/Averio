using System.Runtime.ExceptionServices;
using Averio.Application.Common.Exceptions;
using Averio.Application.Recurring;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Averio.Worker;

public sealed class RecurringBatch(IServiceScopeFactory scopes, ILogger<RecurringBatch> logger, TimeProvider clock)
{
    public async Task RunAsync(CancellationToken ct)
    {
        const int pageSize = 100;
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var unsuccessful = new HashSet<Guid>();
        ExceptionDispatchInfo? transientFailure = null;
        bool advanced;
        do
        {
            advanced = false;
            RecurringCursor? cursor = null;
            while (true)
            {
                IReadOnlyList<RecurringCandidate> candidates;
                await using (var scope = scopes.CreateAsyncScope())
                    candidates = await scope.ServiceProvider.GetRequiredService<IRecurringRepository>()
                        .DueAsync(today, pageSize, ct, cursor);
                if (candidates.Count == 0) break;

                foreach (var candidate in candidates)
                {
                    ct.ThrowIfCancellationRequested();
                    if (unsuccessful.Contains(candidate.RuleId)) continue;

                    try
                    {
                        if (await RunCandidateAsync(candidate, ct))
                            advanced = true;
                        else
                            unsuccessful.Add(candidate.RuleId);
                    }
                    catch (Exception ex) when (NightlyJobRetry.IsTransient(ex))
                    {
                        // Let other due rules run, then retry this batch in the same window.
                        unsuccessful.Add(candidate.RuleId);
                        transientFailure ??= ExceptionDispatchInfo.Capture(ex);
                    }
                }

                if (candidates.Count < pageSize) break;
                cursor = new(candidates[^1].DueOn, candidates[^1].RuleId);
            }
            // A rule may still have overdue occurrences after advancing its due date.
        } while (advanced);

        transientFailure?.Throw();
    }

    private async Task<bool> RunCandidateAsync(RecurringCandidate candidate, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<RecurringHandler>().AutomaticAsync(candidate, ct);
        }
        catch (Exception ex) when (ex is BusinessRuleException or ForbiddenException or NotFoundException or ConflictException or ValidationException or OnboardingRequiredException)
        {
            // Never log rule names, amounts, account details or exception validation payloads.
            logger.LogWarning("Recurring rule {RuleId} needs review", candidate.RuleId);
            try {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<RecurringHandler>().BlockAsync(candidate, ct);
            }
            catch (NotFoundException) { /* Departed membership: never execute again. */ }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception failure) when (!NightlyJobRetry.IsTransient(failure)) { logger.LogError(failure, "Could not mark recurring rule {RuleId}; continuing batch", candidate.RuleId); }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (!NightlyJobRetry.IsTransient(ex)) { logger.LogError(ex, "Recurring rule {RuleId} failed; retrying later", candidate.RuleId); }
        return false;
    }
}
