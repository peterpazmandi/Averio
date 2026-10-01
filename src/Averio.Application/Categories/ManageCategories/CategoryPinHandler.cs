using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;

namespace Averio.Application.Categories.ManageCategories;

public interface ICategoryPinRepository
{
    Task<IReadOnlyDictionary<Guid, int>> GetPinsAsync(Guid memberId, CancellationToken ct);
    Task ReorderAsync(Guid memberId, Guid[] ids, CancellationToken ct);
    Task SetAsync(Guid memberId, Guid categoryId, bool pinned, DateTimeOffset now, CancellationToken ct);
}

public sealed class CategoryPinHandler(ICurrentUser currentUser, IHouseholdRepository households,
    ICategoryManagementRepository categories, ICategoryPinRepository pins, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task ReorderAsync(Guid householdId, IReadOnlyList<Guid>? ids, IReadOnlyList<Guid>? expected, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var household = await households.GetByIdForUserForUpdateAsync(householdId, userId, ct)
            ?? throw new NotFoundException("household", householdId);
        if (!household.IsFinancialSetupCompleted) throw new OnboardingRequiredException();
        var memberId = household.Members.Single(member => member.UserId == userId).Id;
        var current = (await pins.GetPinsAsync(memberId, ct)).OrderBy(p => p.Value).ThenBy(p => p.Key).Select(p => p.Key).ToArray();
        if (expected is null || !current.SequenceEqual(expected))
            throw new ConflictException("category_pin_order_changed", "Error_CategoryPinOrder");
        if (ids is null || ids.Count != current.Length || ids.Distinct().Count() != ids.Count || !ids.ToHashSet().SetEquals(current))
            throw new BusinessRuleException("category_pin_order_invalid", "Error_CategoryPinOrder");
        await pins.ReorderAsync(memberId, ids.ToArray(), ct);
        await transaction.CommitAsync(ct);
    }

    public async Task HandleAsync(Guid householdId, Guid categoryId, bool pinned, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var household = await households.GetByIdForUserForUpdateAsync(householdId, userId, ct)
            ?? throw new NotFoundException("household", householdId);
        if (!household.IsFinancialSetupCompleted) throw new OnboardingRequiredException();
        var category = await categories.GetAsync(householdId, categoryId, ct)
            ?? throw new NotFoundException("category", categoryId);
        if (pinned && category.IsArchived)
            throw new ConflictException("category_archived", "Error_CategoryArchived");
        var memberId = household.Members.Single(member => member.UserId == userId).Id;
        await pins.SetAsync(memberId, categoryId, pinned, clock.GetUtcNow(), ct);
        await transaction.CommitAsync(ct);
    }
}
