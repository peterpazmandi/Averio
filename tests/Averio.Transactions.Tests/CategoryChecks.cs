using Averio.Domain.Categories;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

internal static class CategoryChecks
{
    public static void Invariants()
    {
        var household = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var root = Category.Create(household, " Family ", CategoryKind.Both, "icon", "#10b981", null, now);
        var child = Category.Create(household, "School", CategoryKind.Expense, "icon", "#10B981", root, now);
        Check(root.Name == "Family" && root.AccentColor == "#10B981");
        Check(child.ParentId == root.Id && child.HouseholdId == household);
        Reject(() => Category.Create(household, "Deep", CategoryKind.Expense, "icon", "#10B981", child, now));
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
