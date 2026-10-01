using Averio.Application.Categories.GetCategories;
using Averio.Application.Categories.ManageCategories;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Common;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Application.Transactions.UpdateTransaction;
using Averio.Application.Transactions.VoidTransaction;
using Averio.Domain.Categories;
using Averio.Domain.Common;
using Averio.Domain.Households;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Identity;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class CategoryVerification
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    public static async Task RunAsync(IServiceProvider provider, MutableCurrentUser user, Fixture f, MutableTimeProvider clock)
    {
        user.UserId = f.OwnerUserId;
        string icon;
        string otherIcon;
        Guid systemRoot;
        Guid foreignHousehold;
        Guid foreignCategory;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var icons = await db.Icons.OrderBy(i => i.Id).Take(2).ToListAsync();
            Check(await db.Icons.CountAsync() == 240, "Icon catalog size changed");
            Check(!await db.Icons.AnyAsync(i => i.Name.StartsWith("icon-")), "Icons still have placeholder names");
            Check(await db.Icons.AnyAsync(i => i.Id == 21 && i.Name == "Shopping cart"), "Icon name migration missing");
            icon = icons[0].Value;
            otherIcon = icons[1].Value;
            systemRoot = await db.Categories.Where(c => c.IsBase && c.ParentId == null).Select(c => c.Id).FirstAsync();
            var outsider = new ApplicationUser
            {
                Id = Guid.NewGuid(), UserName = "category-outsider@example.test",
                NormalizedUserName = "CATEGORY-OUTSIDER@EXAMPLE.TEST",
                Email = "category-outsider@example.test", NormalizedEmail = "CATEGORY-OUTSIDER@EXAMPLE.TEST",
                EmailConfirmed = true, DisplayName = "Category outsider", PreferredCulture = "en-US",
                CreatedAtUtc = clock.GetUtcNow(), SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            var other = Household.CreateForUser(outsider.Id, clock.GetUtcNow());
            other.CompleteOnboarding("Other household", "HU", CurrencyCode.Eur, clock.GetUtcNow());
            other.CompleteFinancialSetup(clock.GetUtcNow());
            var foreign = Category.Create(other.Id, "Private household catalog", CategoryKind.Expense, icon, "#10B981", null, clock.GetUtcNow());
            db.Users.Add(outsider);
            db.Households.Add(other);
            db.Categories.Add(foreign);
            await db.SaveChangesAsync();
            foreignHousehold = other.Id;
            foreignCategory = foreign.Id;
        }

        var parent = await Create(provider, f.HouseholdId, new("Family", CategoryKind.Expense, icon, "#10B981", null));
        var child = await Create(provider, f.HouseholdId, new("School meals", CategoryKind.Expense, icon, "#10B981", parent));
        var unpinnedVersion = (await Read(provider, child)).UpdatedAt;
        await Task.WhenAll(Pin(provider, f.HouseholdId, child, true), Pin(provider, f.HouseholdId, child, true));
        await Pin(provider, f.HouseholdId, systemRoot, true);
        await CheckPins(provider, f.HouseholdId, child, systemRoot);
        await ReorderPins(provider, f.HouseholdId, [systemRoot, child], [child, systemRoot]);
        await using (var pinScope = provider.CreateAsyncScope())
        {
            var ordered = await pinScope.ServiceProvider.GetRequiredService<GetCategoriesHandler>().HandleAsync(f.HouseholdId, null, Ct, true);
            Check(ordered.Single(c => c.Id == systemRoot).PinOrder < ordered.Single(c => c.Id == child).PinOrder, "Pin sequence was not saved");
        }
        await Throws<ConflictException>(() => ReorderPins(provider, f.HouseholdId, [child, systemRoot], [child, systemRoot]));
        await Throws<BusinessRuleException>(() => ReorderPins(provider, f.HouseholdId, [child, child], [systemRoot, child]));
        await Throws<BusinessRuleException>(() => ReorderPins(provider, f.HouseholdId, [child, foreignCategory], [systemRoot, child]));
        Check((await Read(provider, child)).UpdatedAt == unpinnedVersion, "Personal pin changed shared category version");
        user.UserId = f.MemberUserId;
        await CheckPins(provider, f.HouseholdId);
        await Pin(provider, f.HouseholdId, parent, true);
        await CheckPins(provider, f.HouseholdId, parent);
        user.UserId = f.OwnerUserId;
        await CheckPins(provider, f.HouseholdId, child, systemRoot);
        await Pin(provider, f.HouseholdId, systemRoot, false);
        await Pin(provider, f.HouseholdId, systemRoot, false);
        await CheckPins(provider, f.HouseholdId, child);
        await Throws<NotFoundException>(() => Pin(provider, f.HouseholdId, foreignCategory, true));
        await Throws<NotFoundException>(() => Pin(provider, foreignHousehold, foreignCategory, true));
        var grandchild = await Create(provider, f.HouseholdId, new("Grandchild", CategoryKind.Expense, icon, "#10B981", child));
        await Throws<BusinessRuleException>(() => Update(provider, f.HouseholdId, parent, new("Cycle", CategoryKind.Expense, icon, "#10B981", grandchild)));
        var anotherRoot = await Create(provider, f.HouseholdId, new("Another root", CategoryKind.Expense, icon, "#10B981", null));
        await Update(provider, f.HouseholdId, parent, new("Family", CategoryKind.Expense, icon, "#10B981", anotherRoot));
        Check((await Read(provider, parent)).ParentId == anotherRoot, "Subtree move failed");
        await Update(provider, f.HouseholdId, parent, new("Family", CategoryKind.Expense, icon, "#10B981", null));
        await Archive(provider, f.HouseholdId, grandchild, true);
        await Throws<BusinessRuleException>(() => Create(provider, f.HouseholdId, new("Wrong kind", CategoryKind.Income, icon, "#10B981", parent)));
        await Throws<BusinessRuleException>(() => Create(provider, f.HouseholdId, new("Foreign", CategoryKind.Expense, icon, "#10B981", foreignCategory)));
        await Throws<BusinessRuleException>(() => Create(provider, f.HouseholdId, new("Bad icon", CategoryKind.Expense, "not-a-catalog-icon", "#10B981", null)));
        await Throws<NotFoundException>(() => Create(provider, foreignHousehold, new("Denied", CategoryKind.Expense, icon, "#10B981", null)));
        await Throws<NotFoundException>(() => Archive(provider, f.HouseholdId, foreignCategory, true));

        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var list = await services.GetRequiredService<GetCategoriesHandler>().HandleAsync(f.HouseholdId, null, Ct, true);
            Check(list.All(c => c.Id != foreignCategory), "Foreign category leaked");
            var system = list.Single(c => c.Id == systemRoot);
            await Throws<ForbiddenException>(() => services.GetRequiredService<CategoryManagementHandler>()
                .SetArchivedAsync(f.HouseholdId, systemRoot, true, system.UpdatedAt, Ct));
        }
        await Throws<BusinessRuleException>(() => Archive(provider, f.HouseholdId, parent, true));
        await Throws<BusinessRuleException>(() => Update(provider, f.HouseholdId, parent, new("Family", CategoryKind.Expense, icon, "#10B981", child)));
        await Throws<BusinessRuleException>(() => Update(provider, f.HouseholdId, child, new("Wrong kind", CategoryKind.Income, icon, "#10B981", parent)));

        Guid transactionId;
        await using (var scope = provider.CreateAsyncScope())
        {
            transactionId = await scope.ServiceProvider.GetRequiredService<CreateTransactionHandler>().HandleAsync(
                new(f.HouseholdId, f.HouseholdAccountId, TransactionKind.Expense, TransactionVisibility.Household,
                    "School", "Original", new DateOnly(2026, 9, 24), [new("Meal", child, 12.5m)], []), Ct);
        }
        var originalCategory = await Read(provider, child);
        await Update(provider, f.HouseholdId, child, new("School lunches", CategoryKind.Expense, otherIcon, "#6366F1", parent));
        await using (var scope = provider.CreateAsyncScope())
        {
            await Throws<ConflictException>(() => scope.ServiceProvider.GetRequiredService<CategoryManagementHandler>()
                .UpdateAsync(f.HouseholdId, child, new("Stale", CategoryKind.Expense, icon, "#10B981", parent),
                    originalCategory.UpdatedAt, Ct));
        }
        await EditTransaction(provider, f, transactionId, child, "Changed note", false);
        await Archive(provider, f.HouseholdId, child, true);
        await CheckPins(provider, f.HouseholdId, child);
        await Throws<ConflictException>(() => Pin(provider, f.HouseholdId, child, true));
        await Throws<BusinessRuleException>(() => EditTransaction(provider, f, transactionId, child, "Wrong kind", false, TransactionKind.Income));
        await EditTransaction(provider, f, transactionId, child, "Archived note", false);
        await EditTransaction(provider, f, transactionId, child, "Invalid duplicate", true);
        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var visible = await services.GetRequiredService<GetCategoriesHandler>().HandleAsync(f.HouseholdId, null, Ct);
            Check(visible.All(c => c.Id != child), "Archived category remains selectable");
            var all = await services.GetRequiredService<GetCategoriesHandler>().HandleAsync(f.HouseholdId, null, Ct, true);
            Check(all.Single(c => c.Id == child).IsArchived, "Management omitted archived category");
            await Throws<BusinessRuleException>(() => services.GetRequiredService<CreateTransactionHandler>().HandleAsync(
                new(f.HouseholdId, f.HouseholdAccountId, TransactionKind.Expense, TransactionVisibility.Household,
                    null, null, new DateOnly(2026, 9, 24), [new("New item", child, 1m)], []), Ct));
        }
        await Archive(provider, f.HouseholdId, parent, true);
        await Throws<BusinessRuleException>(() => Archive(provider, f.HouseholdId, child, false));
        await Archive(provider, f.HouseholdId, parent, false);
        await Archive(provider, f.HouseholdId, child, false);
        await CheckPins(provider, f.HouseholdId, child);
        await Pin(provider, f.HouseholdId, child, false);
        await CheckPins(provider, f.HouseholdId);

        // A shared category is editable by another member, but never in another household.
        user.UserId = f.MemberUserId;
        await Update(provider, f.HouseholdId, child, new("Shared lunches", CategoryKind.Expense, otherIcon, "#6366F1", parent));
        user.UserId = f.OwnerUserId;

        await using (var a = provider.CreateAsyncScope())
        await using (var b = provider.CreateAsyncScope())
        {
            var dbA = a.ServiceProvider.GetRequiredService<AverioDbContext>();
            var dbB = b.ServiceProvider.GetRequiredService<AverioDbContext>();
            var first = await dbA.Categories.SingleAsync(c => c.Id == child);
            var stale = await dbB.Categories.SingleAsync(c => c.Id == child);
            first.Update("First writer", otherIcon, "#6366F1", null, clock.GetUtcNow());
            stale.Update("Stale writer", otherIcon, "#6366F1", null, clock.GetUtcNow());
            await dbA.SaveChangesAsync();
            await Throws<ConflictException>(() => b.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct));
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var ledger = await db.Transactions.Include(t => t.Items).SingleAsync(t => t.Id == transactionId);
            var item = ledger.Items.Single();
            Check(item.CategoryName == "School meals" && item.CategoryIcon == icon, "Historical snapshot changed");
            Check(ledger.Note == "Archived note" && ledger.Amount == 12.5m, "History edit failed");
            var categoryTotal = await db.TransactionAggregates.Where(a => a.HouseholdId == f.HouseholdId
                && a.DimensionType == TransactionAggregateDimension.Category && a.DimensionId == child
                && a.Period == TransactionAggregatePeriod.Month).SumAsync(a => a.ExpenseAmount);
            Check(categoryTotal == 12.5m, "Category edit changed financial totals");
            await scope.ServiceProvider.GetRequiredService<VoidTransactionHandler>()
                .HandleAsync(f.HouseholdId, transactionId, ledger.UpdatedAt, Ct);
        }

        // Retaining an archived Both category remains valid when correcting the transaction kind.
        var both = await Create(provider, f.HouseholdId, new("Original both", CategoryKind.Both, icon, "#10B981", null));
        Guid bothTransactionId;
        await using (var scope = provider.CreateAsyncScope())
        {
            bothTransactionId = await scope.ServiceProvider.GetRequiredService<CreateTransactionHandler>().HandleAsync(
                new(f.HouseholdId, f.HouseholdAccountId, TransactionKind.Expense, TransactionVisibility.Household,
                    null, null, new DateOnly(2026, 9, 24), [new("Original item", both, 12.5m)], []), Ct);
        }
        await Update(provider, f.HouseholdId, both, new("Renamed both", CategoryKind.Both, otherIcon, "#6366F1", null));
        await Archive(provider, f.HouseholdId, both, true);
        await EditTransaction(provider, f, bothTransactionId, both, "Corrected kind", false, TransactionKind.Income);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var ledger = await db.Transactions.Include(t => t.Items).SingleAsync(t => t.Id == bothTransactionId);
            var item = ledger.Items.Single();
            Check(ledger.Kind == TransactionKind.Income && item.CategoryId == both, "Archived Both assignment lost on kind change");
            Check(item.CategoryName == "Original both" && item.CategoryIcon == icon, "Kind change rewrote historical snapshot");
            await scope.ServiceProvider.GetRequiredService<VoidTransactionHandler>()
                .HandleAsync(f.HouseholdId, bothTransactionId, ledger.UpdatedAt, Ct);
        }
        Console.WriteLine("PASS category management, hierarchy, privacy, historical snapshots and concurrency");
    }

    private static async Task ReorderPins(IServiceProvider provider, Guid householdId, Guid[] ids, Guid[] expected)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CategoryPinHandler>().ReorderAsync(householdId, ids, expected, Ct);
    }
    private static async Task Pin(IServiceProvider provider, Guid householdId, Guid id, bool pinned)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CategoryPinHandler>().HandleAsync(householdId, id, pinned, Ct);
    }
    private static async Task CheckPins(IServiceProvider provider, Guid householdId, params Guid[] expected)
    {
        await using var scope = provider.CreateAsyncScope();
        var categories = await scope.ServiceProvider.GetRequiredService<GetCategoriesHandler>().HandleAsync(householdId, null, Ct, true);
        Check(categories.Where(c => c.IsPinned).Select(c => c.Id).ToHashSet().SetEquals(expected), "Pins were lost or leaked across users");
    }

    private static async Task EditTransaction(IServiceProvider provider, Fixture f, Guid id, Guid categoryId, string note, bool duplicate, TransactionKind kind = TransactionKind.Expense)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
        var transaction = await db.Transactions.AsNoTracking().Include(t => t.Items).SingleAsync(t => t.Id == id);
        var original = transaction.Items.Single();
        var item = new TransactionItemInput("Meal", categoryId, 12.5m, original.Id);
        var command = new UpdateTransactionCommand(f.HouseholdId, id, f.HouseholdAccountId,
            kind, TransactionVisibility.Household, "School", note, transaction.OccurredOn,
            duplicate ? [item, item] : [item], [], transaction.UpdatedAt);
        var handler = scope.ServiceProvider.GetRequiredService<UpdateTransactionHandler>();
        if (duplicate) await Throws<BusinessRuleException>(() => handler.HandleAsync(command, Ct));
        else await handler.HandleAsync(command, Ct);
    }

    private static async Task<Guid> Create(IServiceProvider provider, Guid householdId, CategoryInput input)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CategoryManagementHandler>().CreateAsync(householdId, input, Ct);
    }
    private static async Task Update(IServiceProvider provider, Guid householdId, Guid id, CategoryInput input)
    {
        var category = await Read(provider, id);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CategoryManagementHandler>().UpdateAsync(householdId, id, input, category.UpdatedAt, Ct);
    }
    private static async Task Archive(IServiceProvider provider, Guid householdId, Guid id, bool archived)
    {
        var category = await Read(provider, id);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CategoryManagementHandler>().SetArchivedAsync(householdId, id, archived, category.UpdatedAt, Ct);
    }
    private static async Task<Category> Read(IServiceProvider provider, Guid id)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AverioDbContext>().Categories.AsNoTracking().SingleAsync(c => c.Id == id);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task Throws<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
}
