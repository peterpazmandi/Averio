using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Categories;
using Averio.Domain.Transactions;
using Averio.Application.Categories.ManageCategories;

namespace Averio.Application.Categories.GetCategories;

public sealed class GetCategoriesHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    ICategoryRepository categoryRepository, ICategoryPinRepository pins)
{
    public async Task<IReadOnlyList<CategoryResult>> HandleAsync(
        Guid householdId,
        TransactionKind? kind,
        CancellationToken cancellationToken, bool includeArchived = false)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var household = await householdRepository.GetByIdForUserAsync(
            householdId, userId, cancellationToken)
            ?? throw new NotFoundException("household", householdId);

        var categories = await categoryRepository.GetAvailableAsync(
            household.Id, kind, cancellationToken, includeArchived);

        var memberId = household.Members.Single(member => member.UserId == userId).Id;
        var pinned = await pins.GetPinnedIdsAsync(memberId, cancellationToken);
        return categories.Select(category => new CategoryResult(
            category.Id,
            category.ParentId,
            category.SystemKey,
            category.Name,
            category.Kind,
            category.Icon,
            category.AccentColor,
            category.SortOrder,
            category.IsBase, category.IsArchived, category.UpdatedAt, pinned.Contains(category.Id))).ToArray();
    }
}

public sealed record CategoryResult(
    Guid Id,
    Guid? ParentId,
    string? SystemKey,
    string? Name,
    CategoryKind Kind,
    string Icon,
    string AccentColor,
    int SortOrder,
    bool IsSystem, bool IsArchived, DateTimeOffset UpdatedAt, bool IsPinned);

