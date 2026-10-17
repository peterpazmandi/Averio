using Averio.Application.Common.Exceptions;
using Averio.Application.Recurring;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Averio.Worker;

public sealed class RecurringBatch(IServiceScopeFactory scopes, ILogger<RecurringBatch> logger, TimeProvider clock)
{
    private RecurringCursor? cursor;
    public async Task RunAsync(CancellationToken ct)
    {
        IReadOnlyList<RecurringCandidate> candidates;
        await using (var scope = scopes.CreateAsyncScope())
            candidates = await scope.ServiceProvider.GetRequiredService<IRecurringRepository>()
                .DueAsync(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), 100, ct, cursor);
        if (candidates.Count == 0 && cursor is not null)
        {
            cursor = null;
            await using var scope = scopes.CreateAsyncScope();
            candidates = await scope.ServiceProvider.GetRequiredService<IRecurringRepository>()
                .DueAsync(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), 100, ct);
        }
        // Advance even on failed posts; the next pass must give later rules a turn.
        if (candidates.Count > 0) cursor = new(candidates[^1].DueOn, candidates[^1].RuleId);
        foreach (var candidate in candidates)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<RecurringHandler>().AutomaticAsync(candidate, ct);
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
                catch (Exception failure) { logger.LogError(failure, "Could not mark recurring rule {RuleId}; continuing batch", candidate.RuleId); }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogError(ex, "Recurring rule {RuleId} failed; retrying later", candidate.RuleId); }
        }
    }
}
