using System.Collections.Concurrent;
using Averio.Application.Households.Invitations;
using Averio.Application.Identity;
using Averio.Application.Transactions.Common;
using Averio.Domain.Accounts;
using Averio.Domain.Activities;
using Averio.Domain.Common;
using Averio.Domain.Households;
using Averio.Domain.Transactions;
using Averio.Domain.Transfers;
using Averio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Averio.Collaboration.Host;

// This executable is test infrastructure only. Production never references it.
internal static class BrowserHost
{
    public static async Task<int> Main(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("AVERIO_TEST_CONNECTION_STRING");
        if (args is not ["--browser-fixture"] || string.IsNullOrWhiteSpace(connection))
        {
            Console.Error.WriteLine("Require --browser-fixture and AVERIO_TEST_CONNECTION_STRING (local test PostgreSQL).");
            return 2;
        }
        var name = $"averio_browser_{Guid.NewGuid():N}";
        var admin = new NpgsqlConnectionStringBuilder(connection) { Database = "postgres", Pooling = false };
        var created = false;
        try
        {
            Console.WriteLine($"Creating isolated test database {name}");
            await Sql(admin.ConnectionString, $"CREATE DATABASE \"{name}\"");
            created = true;
            var database = new NpgsqlConnectionStringBuilder(connection) { Database = name, Pooling = false };
            Environment.SetEnvironmentVariable("ConnectionStrings__AverioDatabase", database.ConnectionString);
            Environment.SetEnvironmentVariable("Email__Provider", "Development");
            Environment.SetEnvironmentVariable("Email__FrontendBaseUrl", "http://localhost:5173");
            // Migrate before hosted services preload the database-backed key ring.
            await using (var db = new AverioDbContext(new DbContextOptionsBuilder<AverioDbContext>()
                .UseNpgsql(database.ConnectionString).Options))
            {
                await db.Database.MigrateAsync();
            }
            var inbox = new Inbox();
            var clock = new BrowserClock();
            await using var factory = new BrowserFactory(inbox, clock);
            factory.UseKestrel(5180);
            using var client = factory.CreateClient();
            var owner = await Seed(factory.Services, clock, "owner@collaboration.test", "Collaboration home");
            var existing = await Seed(factory.Services, clock, "existing@collaboration.test", "Previous home");
            var dashboard = await Seed(factory.Services, clock, "dashboard@collaboration.test", "Dashboard home");
            var navigation = await Seed(factory.Services, clock, "navigation@collaboration.test", "Navigation home");
            var control = WebApplication.CreateBuilder();
            control.WebHost.UseUrls("http://127.0.0.1:5181");
            await using var app = control.Build();
            app.MapGet("/ready", () => new { owner, existing, dashboard, navigation, password = "Browser-test-password-123!" });
            app.MapGet("/mail", (string email, string kind) => inbox.Messages.TryGetValue((email, kind), out var mail)
                ? Results.Ok(mail) : Results.NotFound());
            app.MapPost("/delivery", (Delivery input) => { inbox.FailedEmail = input.FailedEmail; return Results.NoContent(); });
            app.MapPost("/advance", (Advance input) => { clock.Advance(TimeSpan.FromSeconds(input.Seconds)); return Results.NoContent(); });
            app.MapPost("/shutdown", () => { app.Lifetime.StopApplication(); return Results.NoContent(); });
            await app.RunAsync();
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            if (created)
            {
                NpgsqlConnection.ClearAllPools();
                await Sql(admin.ConnectionString, $"DROP DATABASE \"{name}\" WITH (FORCE)");
            }
        }
    }

    private static async Task Sql(string connection, string sql)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand(sql, db);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object> Seed(IServiceProvider provider, TimeProvider clock, string email, string home)
    {
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var identity = sp.GetRequiredService<IIdentityService>();
        var user = await identity.CreateUserAsync(email, "Browser-test-password-123!", "en-US", default);
        var userId = user.UserId ?? throw new InvalidOperationException("Fixture identity creation failed.");
        var token = await identity.GenerateEmailVerificationTokenAsync(userId, default);
        if (await identity.ConfirmEmailAsync(userId, token!, default) != EmailConfirmationStatus.Succeeded)
            throw new InvalidOperationException("Fixture verification failed.");
        await identity.UpdateDisplayNameAsync(userId, home, default);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var household = Household.CreateForUser(userId, now);
        household.CompleteOnboarding(home, "HU", CurrencyCode.From("EUR"), now);
        household.CompleteFinancialSetup(now);
        var member = household.Members.Single().Id;
        var shared = Account.Create(household.Id, [member], home + " shared", AccountType.Checking,
            AccountScope.Household, AccountVisibility.Household, true, CurrencyCode.From("EUR"), 0, today, now);
        var personal = Account.Create(household.Id, [member], home + " personal", AccountType.Savings,
            AccountScope.Personal, AccountVisibility.Household, true, CurrencyCode.From("EUR"), 0, today, now);
        var secret = Account.Create(household.Id, [member], "SECRET private account", AccountType.Cash,
            AccountScope.Personal, AccountVisibility.Private, false, CurrencyCode.From("HUF"), 1000.1234m, today, now);
        var db = sp.GetRequiredService<AverioDbContext>();
        var category = await db.Categories.Where(x => x.IsBase && x.SystemKey == "income-salary").Select(x => x.Id).SingleAsync();
        var expenseCategory = await db.Categories.Where(x => x.IsBase && x.Kind == Averio.Domain.Categories.CategoryKind.Expense)
            .OrderBy(x => x.SortOrder).FirstAsync();
        var activity = Activity.Create(household.Id, member, "SECRET private activity", today, today, now);
        var sharedTransaction = Transaction.Create(household.Id, shared.Id, member, TransactionKind.Income,
            TransactionVisibility.Household, shared.Currency, "Visible salary", null, today,
            [new("Salary", category, "income-salary", "cash", 150.1234m)], [], now);
        var personalIncome = Transaction.Create(household.Id, personal.Id, member, TransactionKind.Income,
            TransactionVisibility.Household, personal.Currency, "Visible savings", null, today,
            [new("Salary", category, "income-salary", "cash", 80.0001m)], [], now);
        var privateTransaction = Transaction.Create(household.Id, personal.Id, member, TransactionKind.Expense,
            TransactionVisibility.Private, personal.Currency, "SECRET private transaction", "SECRET note", today,
            [new("SECRET item", expenseCategory.Id, expenseCategory.NameReference, expenseCategory.Icon, 7.0001m)], [], now);
        var transfer = Transfer.Create(household.Id, shared.Id, secret.Id, member, shared.Currency, secret.Currency,
            5m, 2000m, today, "SECRET transfer", now);
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.AddRange(household, shared, personal, secret, activity, sharedTransaction, personalIncome, privateTransaction, transfer);
        await db.SaveChangesAsync();
        var aggregates = sp.GetRequiredService<TransactionAggregateWriter>();
        await aggregates.ApplyAsync(sharedTransaction, 1, default);
        await aggregates.ApplyAsync(personalIncome, 1, default);
        await transaction.CommitAsync();
        return new { userId, email, householdId = household.Id, sharedAccountId = shared.Id,
            personalAccountId = personal.Id, privateAccountId = secret.Id, privateTransactionId = privateTransaction.Id,
            sharedTransactionId = sharedTransaction.Id, privateTransferId = transfer.Id, privateActivityId = activity.Id,
            date = today.ToString("yyyy-MM-dd") };
    }
}

internal sealed class BrowserFactory(Inbox inbox, TimeProvider clock) : WebApplicationFactory<global::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IInvitationEmailSender>();
            services.RemoveAll<IEmailVerificationSender>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<IInvitationEmailSender>(inbox);
            services.AddSingleton<IEmailVerificationSender>(inbox);
            services.AddSingleton(clock);
        });
    }
}

internal sealed class Inbox : IInvitationEmailSender, IEmailVerificationSender
{
    public ConcurrentDictionary<(string Email, string Kind), Mail> Messages { get; } = new();
    public string? FailedEmail { get; set; }
    public Task<bool> SendAsync(string email, string token, string householdName, CancellationToken ct)
    {
        if (email == FailedEmail) return Task.FromResult(false);
        Messages[(email, "invitation")] = new($"http://localhost:5173/invitation#{token}");
        return Task.FromResult(true);
    }
    public Task<bool> TrySendAsync(Guid userId, string email, string token, string culture, CancellationToken ct)
    {
        Messages[(email, "verification")] = new($"http://localhost:5173/verify-email?userId={userId}&token={Uri.EscapeDataString(token)}");
        return Task.FromResult(true);
    }
}
internal sealed record Mail(string Url);
internal sealed record Delivery(string? FailedEmail);
internal sealed record Advance(int Seconds);
internal sealed class BrowserClock : TimeProvider
{
    private long _ticks = DateTimeOffset.UtcNow.UtcTicks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
    public void Advance(TimeSpan value) => Interlocked.Add(ref _ticks, value.Ticks);
}
