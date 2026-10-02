using Averio.Domain.Categories;
using FluentValidation;

namespace Averio.Application.Categories.ManageCategories;

public sealed record CategoryInput(string Name, CategoryKind Kind, string Icon, string AccentColor, Guid? ParentId);

public sealed class CategoryInputValidator : AbstractValidator<CategoryInput>
{
    public CategoryInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithErrorCode("Validation_CategoryName").MaximumLength(100).WithErrorCode("Validation_CategoryName");
        RuleFor(x => x.Kind).IsInEnum().WithErrorCode("Validation_CategoryKind");
        RuleFor(x => x.Icon).NotEmpty().WithErrorCode("Validation_CategoryIcon").MaximumLength(32).WithErrorCode("Validation_CategoryIcon");
        RuleFor(x => x.AccentColor).NotEmpty().WithErrorCode("Validation_CategoryColor").Matches(@"\A#[0-9A-Fa-f]{6}\z").WithErrorCode("Validation_CategoryColor");
        RuleFor(x => x.ParentId).Must(id => id is null || id != Guid.Empty).WithErrorCode("Error_CategoryParent");
    }
}

public interface ICategoryManagementRepository
{
    // Serializes structural edits within a household. Financial writes lock selected category rows.
    Task LockHouseholdAsync(Guid householdId, CancellationToken ct);
    Task<Category?> GetAsync(Guid householdId, Guid categoryId, CancellationToken ct);
    Task<bool> HasChildrenAsync(Guid categoryId, bool activeOnly, CancellationToken ct);
    void Add(Category category);
}
