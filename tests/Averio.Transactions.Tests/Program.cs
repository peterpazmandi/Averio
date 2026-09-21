using Averio.Application.Transactions.Common;
using Averio.Domain.Accounts;
using Averio.Domain.Categories;
using Averio.Domain.Common;
using Averio.Domain.Labels;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var checks = new (string Name, Action Run)[]
{
    ("itemized income derives total", ItemizedIncomeDerivesTotal),
    ("reject invalid item amount", RejectInvalidItemAmount),
    ("category snapshot and reusable labels", CategorySnapshotAndReusableLabels),
    ("update recalculates total and advances version", UpdateRecalculatesTotal),
    ("void removes cash-flow contribution", VoidRemovesContribution),
    ("account visibility constrains transaction visibility", VisibilityRules),
    ("year aggregate period exists", YearAggregatePeriodExists),
    ("EF category and icon catalogs", CategoryAndIconModels),
    ("EF reusable label model", ReusableLabelModel),
    ("EF transaction detail model", TransactionDetailModel),
    ("EF dimension aggregate model", DimensionAggregateModel),
};

foreach (var check in checks)
{
    try
    {
        check.Run();
        Console.WriteLine($"PASS {check.Name}");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"FAIL {check.Name}: {exception}");
        return 1;
    }
}

Console.WriteLine($"PASS {checks.Length} transaction regression checks");
return 0;

static readonly Guid GroceriesId =
    Guid.Parse("00000000-0000-4000-8000-000000000011");
static readonly Guid SalaryId =
    Guid.Parse("00000000-0000-4000-8000-000000000091");

static Transaction NewTransaction(
    TransactionKind kind = TransactionKind.Expense,
    TransactionVisibility visibility = TransactionVisibility.Household,
    IReadOnlyCollection<TransactionItemDraft>? items = null,
    IReadOnlyCollection<Guid>? labelIds = null,
    string? merchant = null,
    DateTimeOffset? now = null)
    => Transaction.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        kind,
        visibility,
        CurrencyCode.Eur,
        merchant,
        null,
        new DateOnly(2026, 9, 21),
        items ??
        [
            new TransactionItemDraft(
                "Regression check",
                GroceriesId,
                "Groceries",
                "🛒",
                12.34m)
        ],
        labelIds ?? [],
        now ?? new DateTimeOffset(2026, 9, 21, 6, 0, 0, TimeSpan.Zero));

static void ItemizedIncomeDerivesTotal()
{
    var transaction = NewTransaction(
        kind: TransactionKind.Income,
        items:
        [
            new TransactionItemDraft("Salary", SalaryId, "Salary", "💼", 1234.5678m),
            new TransactionItemDraft("Bonus", SalaryId, "Salary", "💼", 65.4322m),
        ]);

    Equal(TransactionKind.Income, transaction.Kind);
    Equal(1300.0000m, transaction.Amount);
    Equal(2, transaction.Items.Count);
    Equal(CurrencyCode.Eur, transaction.Currency);
    Equal(TransactionStatus.Posted, transaction.Status);
    True(TransactionAccess.ContributesToHouseholdCashFlow(transaction));
}

static void RejectInvalidItemAmount()
{
    Throws<ArgumentOutOfRangeException>(() => NewTransaction(
        items:
        [
            new TransactionItemDraft("Zero", GroceriesId, "Groceries", "🛒", 0m)
        ]));

    Throws<ArgumentOutOfRangeException>(() => NewTransaction(
        items:
        [
            new TransactionItemDraft("Negative", GroceriesId, "Groceries", "🛒", -1m)
        ]));

    Throws<ArgumentOutOfRangeException>(() => NewTransaction(
        items:
        [
            new TransactionItemDraft("Too precise", GroceriesId, "Groceries", "🛒", 1.00001m)
        ]));
}

static void CategorySnapshotAndReusableLabels()
{
    var householdLabelId = Guid.NewGuid();
    var accountLabelId = Guid.NewGuid();

    var transaction = NewTransaction(
        merchant: "Tesco",
        items:
        [
            new TransactionItemDraft("Milk", GroceriesId, "Groceries", "🛒", 3.25m),
            new TransactionItemDraft("Bread", GroceriesId, "Groceries", "🛒", 2.10m),
        ],
        labelIds: [householdLabelId, accountLabelId]);

    Equal("Tesco", transaction.Merchant);
    Equal("Tesco", transaction.Description);
    Equal(5.35m, transaction.Amount);
    Equal(2, transaction.Items.Count);
    Equal(2, transaction.Labels.Count);
    Equal(
        GroceriesId,
        transaction.Items.OrderBy(item => item.Position).First().CategoryId);
    Equal(
        "Groceries",
        transaction.Items.OrderBy(item => item.Position).First().CategoryName);
    Equal(
        "🛒",
        transaction.Items.OrderBy(item => item.Position).First().CategoryIcon);
    True(transaction.Labels.Any(label => label.LabelId == householdLabelId));
    True(transaction.Labels.Any(label => label.LabelId == accountLabelId));
}

static void UpdateRecalculatesTotal()
{
    var now = new DateTimeOffset(2026, 9, 21, 6, 0, 0, TimeSpan.Zero);
    var transaction = NewTransaction(now: now);
    var original = transaction.UpdatedAt;

    transaction.Update(
        transaction.AccountId,
        TransactionKind.Expense,
        TransactionVisibility.Household,
        CurrencyCode.Eur,
        "Updated merchant",
        "note",
        new DateOnly(2026, 9, 20),
        [
            new TransactionItemDraft("One", GroceriesId, "Groceries", "🛒", 10m),
            new TransactionItemDraft("Two", GroceriesId, "Groceries", "🛒", 12.50m),
        ],
        [Guid.NewGuid()],
        now);

    True(transaction.UpdatedAt > original);
    Equal(22.50m, transaction.Amount);
    Equal("Updated merchant", transaction.Description);
    Equal(2, transaction.Items.Count);
    Equal(1, transaction.Labels.Count);
}

static void VoidRemovesContribution()
{
    var transaction = NewTransaction();
    True(TransactionAccess.ContributesToHouseholdCashFlow(transaction));
    transaction.Void(new DateTimeOffset(2026, 9, 21, 7, 0, 0, TimeSpan.Zero));
    Equal(TransactionStatus.Voided, transaction.Status);
    True(!TransactionAccess.ContributesToHouseholdCashFlow(transaction));
}

static void VisibilityRules()
{
    var memberId = Guid.NewGuid();
    var now = new DateTimeOffset(2026, 9, 21, 6, 0, 0, TimeSpan.Zero);

    var household = Account.Create(
        Guid.NewGuid(),
        [],
        "Household",
        AccountType.Checking,
        AccountScope.Household,
        AccountVisibility.Household,
        true,
        CurrencyCode.Eur,
        0m,
        new DateOnly(2026, 9, 20),
        now);

    True(TransactionAccess.IsVisibilityAllowed(
        household,
        TransactionVisibility.Household));
    True(!TransactionAccess.IsVisibilityAllowed(
        household,
        TransactionVisibility.Private));

    var personalPrivate = Account.Create(
        Guid.NewGuid(),
        [memberId],
        "Private",
        AccountType.Checking,
        AccountScope.Personal,
        AccountVisibility.Private,
        false,
        CurrencyCode.Eur,
        0m,
        new DateOnly(2026, 9, 20),
        now);

    True(TransactionAccess.IsVisibilityAllowed(
        personalPrivate,
        TransactionVisibility.Private));
    True(!TransactionAccess.IsVisibilityAllowed(
        personalPrivate,
        TransactionVisibility.Household));
}

static void YearAggregatePeriodExists()
{
    True(Enum.IsDefined(TransactionAggregatePeriod.Year));
}

static void CategoryAndIconModels()
{
    using var context = NewContext();

    var category = context.Model.FindEntityType(typeof(Category))
        ?? throw new Exception("Category entity is missing.");
    Equal("categories", category.GetTableName());
    True(category.FindProperty(nameof(Category.ParentId)) is not null);
    True(category.FindProperty(nameof(Category.Icon)) is not null);
    True(category.GetForeignKeys().All(foreignKey =>
        foreignKey.PrincipalEntityType.ClrType != typeof(IconCatalogEntry)));

    var icon = context.Model.FindEntityType(typeof(IconCatalogEntry))
        ?? throw new Exception("Icon catalog entity is missing.");
    Equal("icons", icon.GetTableName());
}

static void ReusableLabelModel()
{
    using var context = NewContext();

    var label = context.Model.FindEntityType(typeof(Label))
        ?? throw new Exception("Label entity is missing.");
    Equal("labels", label.GetTableName());

    var link = context.Model.FindEntityType(typeof(TransactionLabel))
        ?? throw new Exception("TransactionLabel entity is missing.");
    Equal("transaction_labels", link.GetTableName());
    Equal(2, link.FindPrimaryKey()?.Properties.Count ?? 0);
    True(link.FindProperty(nameof(TransactionLabel.LabelId)) is not null);
}

static void TransactionDetailModel()
{
    using var context = NewContext();

    var item = context.Model.FindEntityType(typeof(TransactionItem))
        ?? throw new Exception("TransactionItem entity is missing.");
    Equal("transaction_items", item.GetTableName());
    True(item.FindProperty(nameof(TransactionItem.CategoryId)) is not null);
    True(item.FindProperty(nameof(TransactionItem.CategoryIcon)) is not null);
    True(item.GetIndexes().Any(index =>
        index.GetDatabaseName() == "ux_transaction_items_transaction_position"
        && index.IsUnique));
}

static void DimensionAggregateModel()
{
    using var context = NewContext();
    var entity = context.Model.FindEntityType(typeof(TransactionAggregate))
        ?? throw new Exception("TransactionAggregate entity is missing.");

    Equal("transaction_aggregates", entity.GetTableName());
    Equal(7, entity.FindPrimaryKey()?.Properties.Count ?? 0);
    True(entity.FindProperty(nameof(TransactionAggregate.DimensionType)) is not null);
    True(entity.FindProperty(nameof(TransactionAggregate.DimensionId)) is not null);
    True(entity.FindProperty(nameof(TransactionAggregate.EntryCount)) is not null);
    True(entity.GetIndexes().Any(index =>
        index.GetDatabaseName() ==
            "ix_transaction_aggregates_household_dimension_period"));
}

static AverioDbContext NewContext()
{
    var options = new DbContextOptionsBuilder<AverioDbContext>()
        .UseNpgsql(
            "Host=localhost;Database=averio_model_only;Username=unused;Password=unused")
        .Options;

    return new AverioDbContext(options);
}

static void True(bool condition)
{
    if (!condition) throw new Exception("Expected condition to be true.");
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected '{expected}', got '{actual}'.");
}

static void Throws<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new Exception($"Expected {typeof(TException).Name}.");
}
