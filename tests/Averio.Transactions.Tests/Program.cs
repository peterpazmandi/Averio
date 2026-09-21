using Averio.Application.Transactions.Common;
using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var checks = new (string Name, Action Run)[]
{
    ("itemized income total", ItemizedIncomeTotal),
    ("reject invalid item amount", RejectInvalidItemAmount),
    ("merchant and scoped labels", MerchantAndScopedLabels),
    ("update recalculates total and advances version", UpdateRecalculatesTotal),
    ("void removes cash-flow contribution", VoidRemovesContribution),
    ("account visibility constrains transaction visibility", VisibilityRules),
    ("EF transaction model", TransactionModel),
    ("EF transaction detail model", TransactionDetailModel),
    ("EF aggregate model", AggregateModel),
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

static Transaction NewTransaction(
    TransactionKind kind = TransactionKind.Expense,
    TransactionVisibility visibility = TransactionVisibility.Household,
    IReadOnlyCollection<TransactionItemDraft>? items = null,
    IReadOnlyCollection<TransactionLabelDraft>? labels = null,
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
            new TransactionItemDraft("Regression check", "Tests", 12.34m)
        ],
        labels ?? [],
        now ?? new DateTimeOffset(2026, 9, 21, 6, 0, 0, TimeSpan.Zero));

static void ItemizedIncomeTotal()
{
    var transaction = NewTransaction(
        kind: TransactionKind.Income,
        items:
        [
            new TransactionItemDraft("Salary", "Salary", 1234.5678m),
            new TransactionItemDraft("Bonus", "Bonus", 65.4322m),
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
        items: [new TransactionItemDraft("Zero", null, 0m)]));

    Throws<ArgumentOutOfRangeException>(() => NewTransaction(
        items: [new TransactionItemDraft("Negative", null, -1m)]));

    Throws<ArgumentOutOfRangeException>(() => NewTransaction(
        items: [new TransactionItemDraft("Too precise", null, 1.00001m)]));
}

static void MerchantAndScopedLabels()
{
    var transaction = NewTransaction(
        merchant: "Tesco",
        items:
        [
            new TransactionItemDraft("Milk", "Groceries", 3.25m),
            new TransactionItemDraft("Bread", "Groceries", 2.10m),
        ],
        labels:
        [
            new TransactionLabelDraft("Family", TransactionLabelScope.Household),
            new TransactionLabelDraft("Daily spending", TransactionLabelScope.Account),
        ]);

    Equal("Tesco", transaction.Merchant);
    Equal("Tesco", transaction.Description);
    Equal(5.35m, transaction.Amount);
    Equal(2, transaction.Items.Count);
    Equal(2, transaction.Labels.Count);

    var householdLabel = transaction.Labels.Single(
        label => label.Scope == TransactionLabelScope.Household);
    var accountLabel = transaction.Labels.Single(
        label => label.Scope == TransactionLabelScope.Account);

    Equal<Guid?>(null, householdLabel.AccountId);
    Equal<Guid?>(transaction.AccountId, accountLabel.AccountId);
    Equal("Groceries", transaction.Items.OrderBy(item => item.Position).First().CategoryName);
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
            new TransactionItemDraft("One", "A", 10m),
            new TransactionItemDraft("Two", "B", 12.50m),
        ],
        [
            new TransactionLabelDraft("Updated", TransactionLabelScope.Household)
        ],
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

    var personalVisible = Account.Create(
        Guid.NewGuid(),
        [memberId],
        "Visible personal",
        AccountType.Checking,
        AccountScope.Personal,
        AccountVisibility.Household,
        true,
        CurrencyCode.Eur,
        0m,
        new DateOnly(2026, 9, 20),
        now);

    True(TransactionAccess.IsVisibilityAllowed(
        personalVisible,
        TransactionVisibility.Private));
    True(TransactionAccess.IsVisibilityAllowed(
        personalVisible,
        TransactionVisibility.Household));
}

static void TransactionModel()
{
    using var context = NewContext();
    var entity = context.Model.FindEntityType(typeof(Transaction))
        ?? throw new Exception("Transaction entity is missing.");

    Equal("transactions", entity.GetTableName());
    True(entity.FindProperty(nameof(Transaction.UpdatedAt))?.IsConcurrencyToken == true);
    True(entity.FindProperty(nameof(Transaction.Merchant)) is not null);
    True(entity.GetKeys().Any(key =>
        key.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(Transaction.HouseholdId), nameof(Transaction.Id)])));
    True(entity.GetIndexes().Any(index =>
        index.GetDatabaseName() == "ix_transactions_household_date_created"));
}

static void TransactionDetailModel()
{
    using var context = NewContext();

    var item = context.Model.FindEntityType(typeof(TransactionItem))
        ?? throw new Exception("TransactionItem entity is missing.");
    Equal("transaction_items", item.GetTableName());
    True(item.GetIndexes().Any(index =>
        index.GetDatabaseName() == "ux_transaction_items_transaction_position"
        && index.IsUnique));

    var label = context.Model.FindEntityType(typeof(TransactionLabel))
        ?? throw new Exception("TransactionLabel entity is missing.");
    Equal("transaction_labels", label.GetTableName());
    True(label.FindProperty(nameof(TransactionLabel.AccountId))?.IsNullable == true);
}

static void AggregateModel()
{
    using var context = NewContext();
    var entity = context.Model.FindEntityType(typeof(TransactionAggregate))
        ?? throw new Exception("TransactionAggregate entity is missing.");

    Equal("transaction_aggregates", entity.GetTableName());
    Equal(5, entity.FindPrimaryKey()?.Properties.Count ?? 0);
    True(entity.GetIndexes().Any(index =>
        index.GetDatabaseName() == "ix_transaction_aggregates_household_period"));
}

static AverioDbContext NewContext()
{
    var options = new DbContextOptionsBuilder<AverioDbContext>()
        .UseNpgsql("Host=localhost;Database=averio_model_only;Username=unused;Password=unused")
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
