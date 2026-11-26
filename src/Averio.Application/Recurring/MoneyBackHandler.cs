using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;

namespace Averio.Application.Recurring;

public sealed record MoneyBackRow(string Kind, Guid Id, string Description, string? Provider, string? Merchant,
    string Currency, decimal Amount, decimal? Outstanding, DateOnly Date, string Status, Guid? SourceId, Guid? CreditId, bool CanModify);

public interface IMoneyBackRepository
{
    Task<IReadOnlyList<MoneyBackRow>> ListAsync(Guid householdId, Guid memberId, int offset, int take, string? status, CancellationToken ct);
}

public sealed class MoneyBackHandler(ICurrentUser user, IHouseholdRepository households, IMoneyBackRepository repository)
{
    public async Task<(IReadOnlyList<MoneyBackRow> Items, bool HasMore)> ListAsync(Guid householdId, int offset, int limit, string? status, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new AuthenticationFailedException();
        var household = await households.GetByIdForUserAsync(householdId, userId, ct) ?? throw new NotFoundException("household", householdId);
        if (!household.IsFinancialSetupCompleted) throw new OnboardingRequiredException();
        var member = household.Members.Single(x => x.UserId == userId && x.LeftAtUtc == null);
        var size = Math.Clamp(limit, 1, 100);
        var rows = await repository.ListAsync(householdId, member.Id, Math.Max(0, offset), size + 1, status, ct);
        return (rows.Take(size).ToArray(), rows.Count > size);
    }
}
