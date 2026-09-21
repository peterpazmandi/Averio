using Averio.Application.Transactions.Common;
using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var checks = new (string Name, Action Run)[]
{
    ("create income", CreateIncome),
    ("reject non-positive amount", RejectNonPositiveAmount),
    ("update advances concurrency version", UpdateAdvancesVersion),
    ("void removes cash-flow contribution", VoidRemovesContribution),
    ("account visibility constrains transaction visibility", VisibilityRules),
    ("EF transaction model", TransactionModel),
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
    decimal amount = 12.34m,
    DateTimeOffset? now = null)
    => Transaction.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        kind,
        visibility,
        amount,
        CurrencyCode.Eur,
        "Regression check",
        null,
        new DateOnly(2026, 9, 21),
        now ?? new DateTimeOffset(2026, 9, 21, 6, 0, 0, TimeSpan.Zero));

static void CreateIncome()
{
    var transaction = NewTransaction(TransactionKind.Income, amount: 1234.5678m);
    Equal(TransactionKind.Income, transaction.Kind);
    Equal(1234.5678m, transaction.Amount);
    Equal(CurrencyCode.Eur, transaction.Currency);
    Equal(TransactionStatus.Posted, transaction.Status);
    True(TransactionAccess.ContributesToHouseholdCashFlow(transaction));
}

static void RejectNonPositiveAmount()
{
    Throws<ArgumentOutOfRangeException>(() => NewTransaction(amount: 0m));
    Throws<ArgumentOutOfRangeException>(() => NewTransaction(amount: -1m));
    Throws<ArgumentOutOfRangeException>(() => NewTransaction(amount: 1.00001m));
}

static void UpdateAdvancesVersion()
{
    var now = new DateTimeOffset(2026, 9, 21, 6, 0, 0, TimeSpan.Zero);
    var transaction = NewTransaction(now: now);
    var original = transaction.UpdatedAt;

    transaction.Update(
        transaction.AccountId,
        TransactionKind.Expense,
        TransactionVisibility.Household,
        22.50m,
        CurrencyCode.Eur,
        "Changed",
        "note",
        new DateOnly(2026, 9, 20),
        now);

    True(transaction.UpdatedAt > original);
    Equal(22.50m, transaction.Amount);
    Equal("Changed", transaction.Description);
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
    True(entity.GetIndexes().Any(index =>
        index.GetDatabaseName() == "ix_transactions_household_date_created"));
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
