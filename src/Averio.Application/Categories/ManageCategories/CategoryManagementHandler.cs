using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Categories;
using FluentValidation;

namespace Averio.Application.Categories.ManageCategories;

public sealed class CategoryManagementHandler(
    ICurrentUser currentUser, IHouseholdRepository households,
    ICategoryManagementRepository categories, IIconCatalogRepository icons,
    IValidator<CategoryInput> validator, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<Guid> CreateAsync(Guid householdId, CategoryInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        await AuthorizeAsync(householdId, ct);
        await ValidateIconAsync(input.Icon, null, ct);
        var parent = await ParentAsync(householdId, input.ParentId, ct);
        await ValidateParentAsync(householdId, parent, input.Kind, Guid.Empty, ct);
        var category = Category.Create(householdId, input.Name, input.Kind,
            input.Icon, input.AccentColor, parent, clock.GetUtcNow());
        categories.Add(category);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return category.Id;
    }

    public async Task UpdateAsync(Guid householdId, Guid id, CategoryInput input,
        DateTimeOffset expectedUpdatedAt, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        await AuthorizeAsync(householdId, ct);
        var category = await CustomAsync(householdId, id, expectedUpdatedAt, ct);
        if (category.IsArchived) throw new ConflictException("category_archived", "Error_CategoryArchived");
        if (category.Kind != input.Kind)
            throw new BusinessRuleException("category_kind_immutable", "Error_CategoryKindImmutable");
        await ValidateIconAsync(input.Icon, category.Icon, ct);
        var parent = await ParentAsync(householdId, input.ParentId, ct);
        await ValidateParentAsync(householdId, parent, input.Kind, id, ct);
        category.Update(input.Name, input.Icon, input.AccentColor, parent, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task SetArchivedAsync(Guid householdId, Guid id, bool archived,
        DateTimeOffset expectedUpdatedAt, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        await AuthorizeAsync(householdId, ct);
        var category = await CustomAsync(householdId, id, expectedUpdatedAt, ct);
        if (archived && await categories.HasChildrenAsync(id, true, ct))
            throw new BusinessRuleException("category_active_children", "Error_CategoryActiveChildren");
        if (!archived)
        {
            var parent = await ParentAsync(householdId, category.ParentId, ct);
            await ValidateParentAsync(householdId, parent, category.Kind, id, ct);
        }
        category.SetArchived(archived, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task AuthorizeAsync(Guid householdId, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();
        var household = await households.GetByIdForUserAsync(householdId, userId, ct)
            ?? throw new NotFoundException("household", householdId);
        if (!household.IsFinancialSetupCompleted) throw new OnboardingRequiredException();
        // Like household accounts, shared custom categories are managed by household members.
    }

    private async Task<Category> CustomAsync(Guid householdId, Guid id, DateTimeOffset expected, CancellationToken ct)
    {
        var category = await categories.GetAsync(householdId, id, ct)
            ?? throw new NotFoundException("category", id);
        if (category.IsBase) throw new ForbiddenException();
        if (category.UpdatedAt != expected)
            throw new ConflictException("category_changed", "Error_CategoryChanged");
        return category;
    }

    private async Task<Category?> ParentAsync(Guid householdId, Guid? id, CancellationToken ct)
        => id is null ? null : await categories.GetAsync(householdId, id.Value, ct)
            ?? throw new BusinessRuleException("category_parent_invalid", "Error_CategoryParent");

    private async Task ValidateParentAsync(Guid householdId, Category? parent, CategoryKind kind, Guid id, CancellationToken ct)
    {
        if (parent is not null && parent.Kind != CategoryKind.Both && parent.Kind != kind)
            throw new BusinessRuleException("category_parent_invalid", "Error_CategoryParent");
        // The household lock serializes moves. Walk the complete chain to prevent moving
        // a subtree under itself and to reject unavailable ancestors.
        var visited = new HashSet<Guid> { id };
        while (parent is not null)
        {
            if (!visited.Add(parent.Id) || parent.IsArchived)
                throw new BusinessRuleException("category_parent_invalid", "Error_CategoryParent");
            parent = await ParentAsync(householdId, parent.ParentId, ct);
        }
    }

    private async Task ValidateIconAsync(string icon, string? existingIcon, CancellationToken ct)
    {
        if (icon == existingIcon) return;
        if (!(await icons.GetAllAsync(ct)).Any(entry => entry.Value == icon))
            throw new BusinessRuleException("category_icon_invalid", "Validation_CategoryIcon");
    }
}
