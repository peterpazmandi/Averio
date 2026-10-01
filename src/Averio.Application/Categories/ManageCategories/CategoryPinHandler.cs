using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;

namespace Averio.Application.Categories.ManageCategories;

public interface ICategoryPinRepository
{
    Task<IReadOnlySet<Guid>> GetPinnedIdsAsync(Guid memberId, CancellationToken ct);
    Task SetAsync(Guid memberId, Guid categoryId, bool pinned, DateTimeOffset now, CancellationToken ct);
}

public sealed class CategoryPinHandler(ICurrentUser currentUser, IHouseholdRepository households,
    ICategoryManagementRepository categories, ICategoryPinRepository pins, IUnitOfWork unitOfWork, TimeProvider clock)
{
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
