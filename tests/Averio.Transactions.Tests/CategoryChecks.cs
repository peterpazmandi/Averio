using Averio.Domain.Categories;
using Averio.Application.Categories.ManageCategories;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

internal static class CategoryChecks
{
    public static void Invariants()
    {
        var household = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var root = Category.Create(household, " Family ", CategoryKind.Both, "icon", "#10b981", null, now);
        var validator = new CategoryInputValidator();
        foreach (var invalidColor in new[] { "#10B981\n", "#10B981\r\n", "#10B981 ", " #10B981", "#10B98" })
        {
            var validation = validator.Validate(new CategoryInput("Color", CategoryKind.Both, "icon", invalidColor, null));
            Check(!validation.IsValid && validation.Errors.Any(e => e.ErrorCode == "Validation_CategoryColor"));
            Reject(() => Category.Create(household, "Color", CategoryKind.Both, "icon", invalidColor, null, now));
            Reject(() => root.Update("Color", "icon", invalidColor, null, now));
            Check(root.Name == "Family" && root.AccentColor == "#10B981");
        }
        Check(validator.Validate(new CategoryInput("Color", CategoryKind.Both, "icon", "#10b981", null)).IsValid);
        var child = Category.Create(household, "School", CategoryKind.Expense, "icon", "#10B981", root, now);
        Check(root.Name == "Family" && root.AccentColor == "#10B981");
        Check(child.ParentId == root.Id && child.HouseholdId == household);
        var grandchild = Category.Create(household, "Deep", CategoryKind.Expense, "icon", "#10B981", child, now);
        Check(grandchild.ParentId == child.Id);
        Reject(() => Category.Create(Guid.NewGuid(), "Foreign", CategoryKind.Expense, "icon", "#10B981", root, now));
        Reject(() => root.Update("Self", "icon", "#10B981", root, now));
        var version = child.UpdatedAt;
        child.SetArchived(true, now);
        Check(child.UpdatedAt > version);
        Reject(() => child.Update("Archived", "icon", "#10B981", null, now));
        child.SetArchived(false, now);
        Check(!child.IsArchived);
        using var db = new AverioDbContext(new DbContextOptionsBuilder<AverioDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only").Options);
        Check(db.Model.FindEntityType(typeof(Category))!.FindProperty(nameof(Category.UpdatedAt))!.IsConcurrencyToken);
    }
    private static void Check(bool condition) { if (!condition) throw new Exception("Category invariant failed"); }
    private static void Reject(Action action)
    {
        try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; }
        throw new Exception("Invalid category accepted");
    }
}
