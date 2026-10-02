using Averio.Application;
using Averio.Application.Accounts.GetAccounts;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Statistics.GetCashFlow;
using Averio.Application.Statistics.GetCategoryCashFlow;
using Averio.Application.Transactions.Common;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Application.Transactions.GetTransaction;
using Averio.Application.Transactions.GetTransactions;
using Averio.Application.Transactions.UpdateTransaction;
using Averio.Application.Transactions.VoidTransaction;
using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Domain.Households;
using Averio.Domain.Labels;
using Averio.Domain.Transactions;
using Averio.Infrastructure;
using Averio.Infrastructure.Identity;
using Averio.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

const string connectionStringVariable = "AVERIO_TEST_CONNECTION_STRING";
var serverConnectionString = Environment.GetEnvironmentVariable(connectionStringVariable);

if (string.IsNullOrWhiteSpace(serverConnectionString))
{
    Console.Error.WriteLine(
        $"FAIL PostgreSQL verification requires {connectionStringVariable}.");
    return 2;
}

var databaseName = $"averio_hardening_{Guid.NewGuid():N}";
var databaseCreated = false;
var exitCode = 0;

try
{
    var adminBuilder = new NpgsqlConnectionStringBuilder(serverConnectionString)
    {
        Database = "postgres",
        Pooling = false,
    };

    await ExecuteDatabaseCommandAsync(
        adminBuilder.ConnectionString,
        $"CREATE DATABASE \"{databaseName}\"");
    databaseCreated = true;

    var testBuilder = new NpgsqlConnectionStringBuilder(serverConnectionString)
    {
        Database = databaseName,
        Pooling = false,
        IncludeErrorDetail = true,
    };

    await RunVerificationAsync(testBuilder.ConnectionString);
    Console.WriteLine("PASS PostgreSQL transaction foundation verification");
}
catch (Exception exception)
{
    Console.Error.WriteLine($"FAIL PostgreSQL transaction foundation verification: {exception}");
    exitCode = 1;
}
finally
{
    if (databaseCreated)
    {
        try
        {
            NpgsqlConnection.ClearAllPools();
            var adminBuilder = new NpgsqlConnectionStringBuilder(serverConnectionString)
            {
                Database = "postgres",
                Pooling = false,
            };

            await ExecuteDatabaseCommandAsync(
                adminBuilder.ConnectionString,
                $"DROP DATABASE \"{databaseName}\" WITH (FORCE)");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"FAIL could not remove isolated database '{databaseName}': {exception}");
            exitCode = 1;
        }
    }
}

return exitCode;

static async Task ExecuteDatabaseCommandAsync(
    string connectionString,
    string commandText)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand(commandText, connection);
    await command.ExecuteNonQueryAsync();
}

static async Task RunVerificationAsync(string connectionString)
{
    var currentUser = new MutableCurrentUser();
    var clock = new MutableTimeProvider(
        new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero));

    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:AverioDatabase"] = connectionString,
            ["Email:Provider"] = "Development",
            ["Email:FrontendBaseUrl"] = "http://localhost:5173",
            ["Email:SenderEmail"] = "tests@averioapp.com",
            ["Email:SenderName"] = "Averio Tests",
        })
        .Build();

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddApplication();
    services.AddInfrastructure(configuration);
    services.AddSingleton<TransferReadInterceptor>();
    services.ConfigureDbContext<AverioDbContext>((sp, options) =>
        options.AddInterceptors(sp.GetRequiredService<TransferReadInterceptor>()), ServiceLifetime.Singleton);
    services.AddSingleton<ICurrentUser>(currentUser);
    services.AddSingleton<TimeProvider>(clock);

    await using var provider = services.BuildServiceProvider(
        new ServiceProviderOptions
        {
            ValidateScopes = true,
        });

    await CheckAsync(
        "migration chain and seed catalogs",
        () => VerifyMigrationsAndSeedsAsync(provider));

    var fixture = await SeedFixtureAsync(provider, clock.GetUtcNow());
    currentUser.UserId = fixture.OwnerUserId;

    await CheckAsync(
        "transaction and aggregate writes roll back atomically",
        () => VerifyAtomicRollbackAsync(provider, currentUser, fixture));

    var transactions = await CreateTransactionsAsync(provider, fixture);

    await CheckAsync(
        "transaction detail, search, aggregates and balances persist",
        () => VerifyCreatedStateAsync(
            provider,
            currentUser,
            fixture,
            transactions));

    await CheckAsync(
        "private account data stays hidden from non-owners",
        () => VerifyPrivacyAsync(
            provider,
            currentUser,
            fixture,
            transactions));

    currentUser.UserId = fixture.OwnerUserId;
    clock.Advance(TimeSpan.FromDays(20));

    await CheckAsync(
        "updates move aggregates and reject stale versions",
        () => VerifyUpdateAsync(
            provider,
            fixture,
            transactions.ExpenseId));

    await CheckAsync(
        "voiding reverses aggregates and remains idempotent",
        () => VerifyVoidAsync(
            provider,
            clock,
            fixture,
            transactions.ExpenseId));

    await CheckAsync(
        "PostgreSQL enforces optimistic concurrency",
        () => VerifyDatabaseConcurrencyAsync(provider, clock, fixture));

    await TransferVerification.RunAsync(provider, currentUser, fixture, clock);
    await TransferAccountConcurrencyVerification.RunAsync(provider, fixture, clock);
    await CategoryVerification.RunAsync(provider, currentUser, fixture, clock);
}

static async Task CheckAsync(string name, Func<Task> check)
{
    try
    {
        await check();
        Console.WriteLine($"PASS {name}");
    }
    catch
    {
        Console.Error.WriteLine($"FAIL {name}");
        throw;
    }
}

static async Task VerifyMigrationsAndSeedsAsync(IServiceProvider provider)
{
    await using var scope = provider.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<AverioDbContext>();

    await context.Database.MigrateAsync();

    var pending = await context.Database.GetPendingMigrationsAsync();
    Equal(0, pending.Count());

    var applied = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
    True(applied.Contains("20260921234000_LocalizeSystemCategories"));
    Equal(240, await context.Icons.CountAsync());
    Equal(100, await context.Categories.CountAsync());
    Equal(
        100,
        await context.Categories.CountAsync(category =>
            category.IsBase
            && category.SystemKey != null
            && category.Name == null));
}

static async Task<Fixture> SeedFixtureAsync(
    IServiceProvider provider,
    DateTimeOffset now)
{
    await using var scope = provider.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<AverioDbContext>();

    var ownerUser = NewUser("owner@hardening.test", "Owner", now);
    var memberUser = NewUser("member@hardening.test", "Member", now);
    context.Users.AddRange(ownerUser, memberUser);

    var household = Household.CreateForUser(ownerUser.Id, now);
    household.CompleteOnboarding(
        "Hardening Household",
        "DE",
        CurrencyCode.Eur,
        now);
    household.CompleteFinancialSetup(now);

    var ownerMemberId = household.Members.Single().Id;
    var householdAccount = Account.Create(
        household.Id,
        [],
        "Household Current",
        AccountType.Checking,
        AccountScope.Household,
        AccountVisibility.Household,
        true,
        CurrencyCode.Eur,
        1000m,
        new DateOnly(2026, 9, 1),
        now);

    var privateAccount = Account.Create(
        household.Id,
        [ownerMemberId],
        "Owner Private",
        AccountType.Checking,
        AccountScope.Personal,
        AccountVisibility.Private,
        false,
        CurrencyCode.Eur,
        500m,
        new DateOnly(2026, 9, 1),
        now);

    var householdLabel = Label.Create(
        household.Id,
        null,
        LabelScope.Household,
        "Family",
        now);
    var privateLabel = Label.Create(
        household.Id,
        privateAccount.Id,
        LabelScope.Account,
        "Owner only",
        now);

    context.Households.Add(household);
    context.Accounts.AddRange(householdAccount, privateAccount);
    context.Labels.AddRange(householdLabel, privateLabel);
    await context.SaveChangesAsync();

    var memberId = Guid.NewGuid();
    await context.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO household_members
            (id, household_id, user_id, role, joined_at_utc)
        VALUES
            ({memberId}, {household.Id}, {memberUser.Id}, {"member"}, {now});
        """);

    return new Fixture(
        ownerUser.Id,
        memberUser.Id,
        household.Id,
        ownerMemberId,
        memberId,
        householdAccount.Id,
        privateAccount.Id,
        householdLabel.Id,
        privateLabel.Id);
}

static ApplicationUser NewUser(
    string email,
    string displayName,
    DateTimeOffset now)
{
    var normalized = email.ToUpperInvariant();
    return new ApplicationUser
    {
        Id = Guid.NewGuid(),
        UserName = email,
        NormalizedUserName = normalized,
        Email = email,
        NormalizedEmail = normalized,
        EmailConfirmed = true,
        DisplayName = displayName,
        PreferredCulture = "en-US",
        CreatedAtUtc = now,
        LockoutEnabled = true,
        SecurityStamp = Guid.NewGuid().ToString("N"),
        ConcurrencyStamp = Guid.NewGuid().ToString("N"),
    };
}

static async Task VerifyAtomicRollbackAsync(
    IServiceProvider provider,
    MutableCurrentUser currentUser,
    Fixture fixture)
{
    currentUser.UserId = fixture.OwnerUserId;

    await using (var scope = provider.CreateAsyncScope())
    {
        var services = scope.ServiceProvider;
        var handler = new CreateTransactionHandler(
            services.GetRequiredService<ICurrentUser>(),
            services.GetRequiredService<IHouseholdRepository>(),
            services.GetRequiredService<IAccountRepository>(),
            services.GetRequiredService<ITransactionRepository>(),
            services.GetRequiredService<IValidator<TransactionInput>>(),
            services.GetRequiredService<TransactionReferenceResolver>(),
            new TransactionAggregateWriter(new FailingAggregateRepository()),
            services.GetRequiredService<IUnitOfWork>(),
            services.GetRequiredService<TimeProvider>());

        await ThrowsAsync<AggregateWriteFailedException>(() => handler.HandleAsync(
            new CreateTransactionCommand(
                fixture.HouseholdId,
                fixture.HouseholdAccountId,
                TransactionKind.Expense,
                TransactionVisibility.Household,
                "ROLLBACK SENTINEL",
                null,
                new DateOnly(2026, 9, 20),
                [new TransactionItemInput("Rollback", TestIds.Groceries, 1m)],
                [fixture.HouseholdLabelId]),
            CancellationToken.None));
    }

    await using var verificationScope = provider.CreateAsyncScope();
    var context = verificationScope.ServiceProvider
        .GetRequiredService<AverioDbContext>();
    Equal(
        0,
        await context.Transactions.CountAsync(transaction =>
            transaction.Merchant == "ROLLBACK SENTINEL"));
    Equal(0, await context.TransactionAggregates.CountAsync());
}

static async Task<CreatedTransactions> CreateTransactionsAsync(
    IServiceProvider provider,
    Fixture fixture)
{
    var expenseId = await CreateTransactionAsync(
        provider,
        new CreateTransactionCommand(
            fixture.HouseholdId,
            fixture.HouseholdAccountId,
            TransactionKind.Expense,
            TransactionVisibility.Household,
            "Market",
            "Weekly shop",
            new DateOnly(2026, 9, 21),
            [
                new TransactionItemInput("Milk", TestIds.Groceries, 12.34m),
                new TransactionItemInput("Bread", TestIds.Groceries, 7.66m),
            ],
            [fixture.HouseholdLabelId]));

    var incomeId = await CreateTransactionAsync(
        provider,
        new CreateTransactionCommand(
            fixture.HouseholdId,
            fixture.HouseholdAccountId,
            TransactionKind.Income,
            TransactionVisibility.Household,
            "Employer",
            null,
            new DateOnly(2026, 9, 21),
            [new TransactionItemInput("Salary", TestIds.Salary, 1000m)],
            []));

    var privateId = await CreateTransactionAsync(
        provider,
        new CreateTransactionCommand(
            fixture.HouseholdId,
            fixture.PrivateAccountId,
            TransactionKind.Expense,
            TransactionVisibility.Private,
            "Private purchase",
            null,
            new DateOnly(2026, 9, 21),
            [new TransactionItemInput("Personal", TestIds.Groceries, 50m)],
            [fixture.PrivateLabelId]));

    return new CreatedTransactions(expenseId, incomeId, privateId);
}

static async Task<Guid> CreateTransactionAsync(
    IServiceProvider provider,
    CreateTransactionCommand command)
{
    await using var scope = provider.CreateAsyncScope();
    return await scope.ServiceProvider
        .GetRequiredService<CreateTransactionHandler>()
        .HandleAsync(command, CancellationToken.None);
}

static async Task VerifyCreatedStateAsync(
    IServiceProvider provider,
    MutableCurrentUser currentUser,
    Fixture fixture,
    CreatedTransactions transactions)
{
    currentUser.UserId = fixture.OwnerUserId;

    await using var scope = provider.CreateAsyncScope();
    var services = scope.ServiceProvider;
    var detail = await services.GetRequiredService<GetTransactionHandler>()
        .HandleAsync(
            fixture.HouseholdId,
            transactions.ExpenseId,
            CancellationToken.None);

    Equal(20m, detail.Amount);
    Equal(2, detail.Items.Count);
    Equal(1, detail.Labels.Count);
    Equal("groceries", detail.Items[0].CategoryName);

    var search = await services.GetRequiredService<GetTransactionsHandler>()
        .HandleAsync(
            fixture.HouseholdId,
            null,
            null,
            null,
            null,
            "Family",
            10,
            null,
            null,
            null,
            CancellationToken.None);
    Equal(1, search.Items.Count);
    Equal(transactions.ExpenseId, search.Items[0].Id);

    var cashFlow = await services.GetRequiredService<GetCashFlowHandler>()
        .HandleAsync(
            fixture.HouseholdId,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            TransactionAggregatePeriod.Month,
            CancellationToken.None);
    Equal(1, cashFlow.Periods.Count);
    Equal(1000m, cashFlow.Periods[0].IncomeAmount);
    Equal(20m, cashFlow.Periods[0].ExpenseAmount);
    Equal(2, cashFlow.Periods[0].TransactionCount);

    var categories = await services
        .GetRequiredService<GetCategoryCashFlowHandler>()
        .HandleAsync(
            fixture.HouseholdId,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            TransactionAggregatePeriod.Month,
            CancellationToken.None);
    var groceries = categories.Periods.Single(row =>
        row.CategoryId == TestIds.Groceries);
    Equal(20m, groceries.ExpenseAmount);
    Equal(2, groceries.ItemCount);
    var salary = categories.Periods.Single(row =>
        row.CategoryId == TestIds.Salary);
    Equal(1000m, salary.IncomeAmount);
    Equal(1, salary.ItemCount);

    var accounts = (await services.GetRequiredService<GetAccountsHandler>()
        .HandleAsync(fixture.HouseholdId, false, CancellationToken.None))
        .ToArray();
    Equal(2, accounts.Length);
    Equal(
        1980m,
        accounts.Single(account => account.Id == fixture.HouseholdAccountId)
            .CurrentBalanceAmount);
    Equal(
        450m,
        accounts.Single(account => account.Id == fixture.PrivateAccountId)
            .CurrentBalanceAmount);
}

static async Task VerifyPrivacyAsync(
    IServiceProvider provider,
    MutableCurrentUser currentUser,
    Fixture fixture,
    CreatedTransactions transactions)
{
    currentUser.UserId = fixture.MemberUserId;

    await using var scope = provider.CreateAsyncScope();
    var services = scope.ServiceProvider;
    var accounts = (await services.GetRequiredService<GetAccountsHandler>()
        .HandleAsync(fixture.HouseholdId, false, CancellationToken.None))
        .ToArray();
    Equal(1, accounts.Length);
    Equal(fixture.HouseholdAccountId, accounts[0].Id);
    Equal(1980m, accounts[0].CurrentBalanceAmount);

    var page = await services.GetRequiredService<GetTransactionsHandler>()
        .HandleAsync(
            fixture.HouseholdId,
            null,
            null,
            null,
            null,
            null,
            10,
            null,
            null,
            null,
            CancellationToken.None);
    Equal(2, page.Items.Count);
    True(page.Items.All(item => item.Id != transactions.PrivateId));

    await ThrowsAsync<NotFoundException>(() => services
        .GetRequiredService<GetTransactionHandler>()
        .HandleAsync(
            fixture.HouseholdId,
            transactions.PrivateId,
            CancellationToken.None));
}

static async Task VerifyUpdateAsync(
    IServiceProvider provider,
    Fixture fixture,
    Guid transactionId)
{
    DateTimeOffset originalVersion;
    await using (var readScope = provider.CreateAsyncScope())
    {
        originalVersion = (await readScope.ServiceProvider
            .GetRequiredService<GetTransactionHandler>()
            .HandleAsync(
                fixture.HouseholdId,
                transactionId,
                CancellationToken.None))
            .UpdatedAt;
    }

    var command = new UpdateTransactionCommand(
        fixture.HouseholdId,
        transactionId,
        fixture.HouseholdAccountId,
        TransactionKind.Expense,
        TransactionVisibility.Household,
        "October Market",
        "Moved and corrected",
        new DateOnly(2026, 10, 5),
        [new TransactionItemInput("Groceries", TestIds.Groceries, 30m)],
        [fixture.HouseholdLabelId],
        originalVersion);

    await using (var updateScope = provider.CreateAsyncScope())
    {
        await updateScope.ServiceProvider
            .GetRequiredService<UpdateTransactionHandler>()
            .HandleAsync(command, CancellationToken.None);
    }

    await using var verificationScope = provider.CreateAsyncScope();
    var services = verificationScope.ServiceProvider;
    var updated = await services.GetRequiredService<GetTransactionHandler>()
        .HandleAsync(fixture.HouseholdId, transactionId, CancellationToken.None);
    Equal(30m, updated.Amount);
    Equal(1, updated.Items.Count);
    Equal(new DateOnly(2026, 10, 5), updated.OccurredOn);
    True(updated.UpdatedAt > originalVersion);

    var conflict = await ThrowsAsync<ConflictException>(() => services
        .GetRequiredService<UpdateTransactionHandler>()
        .HandleAsync(command, CancellationToken.None));
    Equal("transaction_changed", conflict.Code);

    var cashFlow = await services.GetRequiredService<GetCashFlowHandler>()
        .HandleAsync(
            fixture.HouseholdId,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 10, 31),
            TransactionAggregatePeriod.Month,
            CancellationToken.None);
    var september = cashFlow.Periods.Single(row =>
        row.PeriodStart == new DateOnly(2026, 9, 1));
    Equal(1000m, september.IncomeAmount);
    Equal(0m, september.ExpenseAmount);
    Equal(1, september.TransactionCount);
    var october = cashFlow.Periods.Single(row =>
        row.PeriodStart == new DateOnly(2026, 10, 1));
    Equal(30m, october.ExpenseAmount);
    Equal(1, october.TransactionCount);

    var accounts = (await services.GetRequiredService<GetAccountsHandler>()
        .HandleAsync(fixture.HouseholdId, false, CancellationToken.None))
        .ToArray();
    Equal(
        1970m,
        accounts.Single(account => account.Id == fixture.HouseholdAccountId)
            .CurrentBalanceAmount);
}

static async Task VerifyVoidAsync(
    IServiceProvider provider,
    MutableTimeProvider clock,
    Fixture fixture,
    Guid transactionId)
{
    clock.Advance(TimeSpan.FromMinutes(1));

    DateTimeOffset expectedVersion;
    await using (var readScope = provider.CreateAsyncScope())
    {
        expectedVersion = (await readScope.ServiceProvider
            .GetRequiredService<GetTransactionHandler>()
            .HandleAsync(
                fixture.HouseholdId,
                transactionId,
                CancellationToken.None))
            .UpdatedAt;
    }

    await using (var voidScope = provider.CreateAsyncScope())
    {
        await voidScope.ServiceProvider
            .GetRequiredService<VoidTransactionHandler>()
            .HandleAsync(
                fixture.HouseholdId,
                transactionId,
                expectedVersion,
                CancellationToken.None);
    }

    await using var verificationScope = provider.CreateAsyncScope();
    var services = verificationScope.ServiceProvider;
    var detail = await services.GetRequiredService<GetTransactionHandler>()
        .HandleAsync(fixture.HouseholdId, transactionId, CancellationToken.None);
    Equal(TransactionStatus.Voided, detail.Status);

    await services.GetRequiredService<VoidTransactionHandler>()
        .HandleAsync(
            fixture.HouseholdId,
            transactionId,
            detail.UpdatedAt,
            CancellationToken.None);

    var cashFlow = await services.GetRequiredService<GetCashFlowHandler>()
        .HandleAsync(
            fixture.HouseholdId,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 10, 31),
            TransactionAggregatePeriod.Month,
            CancellationToken.None);
    var october = cashFlow.Periods.Single(row =>
        row.PeriodStart == new DateOnly(2026, 10, 1));
    Equal(0m, october.ExpenseAmount);
    Equal(0, october.TransactionCount);

    var accounts = (await services.GetRequiredService<GetAccountsHandler>()
        .HandleAsync(fixture.HouseholdId, false, CancellationToken.None))
        .ToArray();
    Equal(
        2000m,
        accounts.Single(account => account.Id == fixture.HouseholdAccountId)
            .CurrentBalanceAmount);
}

static async Task VerifyDatabaseConcurrencyAsync(
    IServiceProvider provider,
    MutableTimeProvider clock,
    Fixture fixture)
{
    clock.Advance(TimeSpan.FromMinutes(1));

    await using (var firstScope = provider.CreateAsyncScope())
    await using (var secondScope = provider.CreateAsyncScope())
    {
        var firstContext = firstScope.ServiceProvider
            .GetRequiredService<AverioDbContext>();
        var secondContext = secondScope.ServiceProvider
            .GetRequiredService<AverioDbContext>();

        var first = await firstContext.Accounts.SingleAsync(account =>
            account.Id == fixture.HouseholdAccountId);
        var second = await secondContext.Accounts.SingleAsync(account =>
            account.Id == fixture.HouseholdAccountId);

        first.Update(
            "Household Current A",
            AccountType.Checking,
            [],
            AccountVisibility.Household,
            true,
            clock.GetUtcNow());
        second.Update(
            "Household Current B",
            AccountType.Checking,
            [],
            AccountVisibility.Household,
            true,
            clock.GetUtcNow());

        await firstContext.SaveChangesAsync();
        await ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondContext.SaveChangesAsync());
    }

    await using var verificationScope = provider.CreateAsyncScope();
    var context = verificationScope.ServiceProvider
        .GetRequiredService<AverioDbContext>();
    Equal(
        "Household Current A",
        await context.Accounts
            .Where(account => account.Id == fixture.HouseholdAccountId)
            .Select(account => account.Name)
            .SingleAsync());
}

static async Task<TException> ThrowsAsync<TException>(Func<Task> action)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException exception)
    {
        return exception;
    }

    throw new Exception($"Expected {typeof(TException).Name}.");
}

static void True(bool condition)
{
    if (!condition)
        throw new Exception("Expected condition to be true.");
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected '{expected}', got '{actual}'.");
}

internal sealed class MutableCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
}

internal sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration)
        => _utcNow = _utcNow.Add(duration);
}

internal sealed class FailingAggregateRepository : ITransactionAggregateRepository
{
    public Task ApplyDeltaAsync(
        Guid householdId,
        Guid accountId,
        TransactionAggregateDimension dimensionType,
        Guid dimensionId,
        string currency,
        DateOnly occurredOn,
        TransactionKind kind,
        decimal amountDelta,
        int entryCountDelta,
        CancellationToken cancellationToken)
        => throw new AggregateWriteFailedException();

    public Task<IReadOnlyList<CashFlowAggregateReadModel>> GetCashFlowAsync(
        Guid householdId,
        DateOnly from,
        DateOnly to,
        TransactionAggregatePeriod period,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CashFlowAggregateReadModel>>([]);

    public Task<IReadOnlyList<CategoryCashFlowAggregateReadModel>>
        GetCategoryCashFlowAsync(
            Guid householdId,
            DateOnly from,
            DateOnly to,
            TransactionAggregatePeriod period,
            CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CategoryCashFlowAggregateReadModel>>([]);
}

internal sealed class AggregateWriteFailedException : Exception
{
}

internal static class TestIds
{
    public static readonly Guid Groceries =
        Guid.Parse("00000000-0000-4000-8000-000000000011");

    public static readonly Guid Salary =
        Guid.Parse("00000000-0000-4000-8000-000000000092");
}

internal sealed record Fixture(
    Guid OwnerUserId,
    Guid MemberUserId,
    Guid HouseholdId,
    Guid OwnerMemberId,
    Guid MemberId,
    Guid HouseholdAccountId,
    Guid PrivateAccountId,
    Guid HouseholdLabelId,
    Guid PrivateLabelId);

internal sealed record CreatedTransactions(
    Guid ExpenseId,
    Guid IncomeId,
    Guid PrivateId);
