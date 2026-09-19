using System.Text.Json;
using Averio.Application.Accounts.ArchiveAccount;
using Averio.Application.Accounts.Common;
using Averio.Application.Accounts.CreateAccount;
using Averio.Application.Accounts.RestoreAccount;
using Averio.Application.Accounts.UpdateAccount;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Contracts.Accounts;
using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Domain.Households;
using Averio.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

// Dependency-free executable regression suite: dotnet run --project tests/Averio.Accounts.Tests
var passed = 0;
await Test("Currency, scope and balance anchor survive editing", async () => {
    var f = new Fixture();
    var id = f.Account.Id;
    await f.Update().HandleAsync(f.Command() with { Name = "  Savings  ", Type = AccountType.Savings }, default);
    Check(f.Account.Name == "Savings" && f.Account.Type == AccountType.Savings);
    Check(f.Account.Id == id && f.Account.Currency.Value == "EUR" && f.Account.Scope == AccountScope.Personal);
    Check(f.Account.BalanceAnchorAmount == 123.4567m && f.Account.BalanceAnchorDate == new DateOnly(2026, 9, 1));
    Check(f.Work.Saves == 1);
});
await Test("Personal holder cannot remove their own access", async () => {
    var f = new Fixture();
    // Invitations are not implemented yet; seed another same-household member for this case.
    var peer = (HouseholdMember)typeof(HouseholdMember).GetMethod("CreateOwner",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
        .Invoke(null, [f.Household.Id, Guid.NewGuid(), f.Now])!;
    ((ICollection<HouseholdMember>)f.Household.Members).Add(peer);
    await Throws<ValidationException>(() => f.Update().HandleAsync(f.Command() with { OwnerHouseholdMemberIds = [peer.Id] }, default));
    Check(f.Work.Saves == 0);
});
await Test("Foreign household holders rejected", async () => {
    var f = new Fixture();
    await Throws<ValidationException>(() => f.Update().HandleAsync(f.Command() with { OwnerHouseholdMemberIds = [f.Member.Id, Guid.NewGuid()] }, default));
    Check(f.Work.Saves == 0);
});
await Test("Duplicate holders rejected without mutation", async () => {
    var f = new Fixture();
    await Throws<ValidationException>(() => f.Update().HandleAsync(f.Command() with { OwnerHouseholdMemberIds = [f.Member.Id, f.Member.Id] }, default));
    Check(f.Account.Name == "Personal" && f.Work.Saves == 0);
});
await Test("Null holder input produces validation error", async () => {
    var f = new Fixture();
    await Throws<ValidationException>(() => f.Update().HandleAsync(f.Command() with { OwnerHouseholdMemberIds = null! }, default));
});
await Test("Private reporting remains prohibited", async () => {
    var f = new Fixture();
    await Throws<ValidationException>(() => f.Update().HandleAsync(f.Command() with { IncludeInHouseholdReports = true }, default));
    Check(!f.Account.IncludeInHouseholdReports);
});
await Test("Personal shared visibility and reporting are independently editable", async () => {
    var f = new Fixture();
    await f.Update().HandleAsync(f.Command() with { Visibility = AccountVisibility.Household, IncludeInHouseholdReports = false }, default);
    Check(f.Account.Visibility == AccountVisibility.Household && !f.Account.IncludeInHouseholdReports);
    await f.Update().HandleAsync(f.Command() with { Visibility = AccountVisibility.Household, IncludeInHouseholdReports = true }, default);
    Check(f.Account.IncludeInHouseholdReports);
});
await Test("Stale update fails with conflict before mutation", async () => {
    var f = new Fixture();
    await Throws<ConflictException>(() => f.Update().HandleAsync(f.Command() with { ExpectedUpdatedAt = f.Now.AddDays(-1) }, default));
    Check(f.Work.Saves == 0 && f.Account.Name == "Personal");
});
await Test("Archived account must be restored before editing", async () => {
    var f = new Fixture(); f.Account.Archive(f.Now.AddSeconds(1));
    await Throws<ConflictException>(() => f.Update().HandleAsync(f.Command(), default));
    Check(f.Work.Saves == 0);
});
await Test("Unauthenticated requests fail", async () => {
    var f = new Fixture();
    await Throws<AuthenticationFailedException>(() => f.Update(new User(null)).HandleAsync(f.Command(), default));
    Check(f.Work.Saves == 0);
});
await Test("Cross-household update returns not found", async () => {
    var f = new Fixture();
    await Throws<NotFoundException>(() => f.Update().HandleAsync(f.Command() with { HouseholdId = Guid.NewGuid() }, default));
});
await Test("Invisible account returns not found before permission check", async () => {
    var f = new Fixture(); f.Accounts.Hide = true;
    await Throws<NotFoundException>(() => f.Update().HandleAsync(f.Command(), default));
    Check(f.Work.Saves == 0);
});
await Test("Household administrator cannot edit another member's personal account", async () => {
    var f = new Fixture();
    f.Accounts.Value = Account.Create(f.Household.Id, [Guid.NewGuid()], "Other", AccountType.Cash,
        AccountScope.Personal, AccountVisibility.Household, false, CurrencyCode.From("HUF"), 0, new(2026, 9, 1), f.Now);
    await Throws<ForbiddenException>(() => f.Update().HandleAsync(f.Command(), default));
    Check(!AccountAccess.CanModify(f.Account, f.Member) && f.Work.Saves == 0);
});
await Test("Archive and restore preserve identity and balance and are idempotent", async () => {
    var f = new Fixture();
    var archive = new ArchiveAccountHandler(f.User, f.Households, f.Accounts, f.Work, f.Clock);
    await archive.HandleAsync(f.Household.Id, f.Account.Id, default);
    var at = f.Account.ArchivedAt;
    await archive.HandleAsync(f.Household.Id, f.Account.Id, default);
    Check(f.Account.Status == AccountStatus.Archived && f.Account.ArchivedAt == at);
    var restore = new RestoreAccountHandler(f.User, f.Households, f.Accounts, f.Work, f.Clock);
    await restore.HandleAsync(f.Household.Id, f.Account.Id, default);
    await restore.HandleAsync(f.Household.Id, f.Account.Id, default);
    Check(f.Account.Status == AccountStatus.Active && f.Account.ArchivedAt is null && f.Account.BalanceAnchorAmount == 123.4567m);
});
await Test("Household role does not authorize personal archive or restore", async () => {
    var f = new Fixture();
    f.Accounts.Value = Account.Create(f.Household.Id, [Guid.NewGuid()], "Other", AccountType.Cash,
        AccountScope.Personal, AccountVisibility.Household, false, CurrencyCode.From("EUR"), 0, new(2026, 9, 1), f.Now);
    await Throws<ForbiddenException>(() => new ArchiveAccountHandler(f.User, f.Households, f.Accounts, f.Work, f.Clock)
        .HandleAsync(f.Household.Id, f.Account.Id, default));
    await Throws<ForbiddenException>(() => new RestoreAccountHandler(f.User, f.Households, f.Accounts, f.Work, f.Clock)
        .HandleAsync(f.Household.Id, f.Account.Id, default));
});
await Test("Domain rejects household privacy and preserves previous state", () => {
    var f = new Fixture();
    var a = Account.Create(f.Household.Id, [], "Household", AccountType.Cash, AccountScope.Household,
        AccountVisibility.Household, true, CurrencyCode.From("EUR"), 0, new(2026, 9, 1), f.Now);
    try { a.Update("Invalid", a.Type, [], AccountVisibility.Private, false, f.Now); throw new Exception("Expected failure"); }
    catch (InvalidOperationException) { Check(a.Name == "Household"); }
    return Task.CompletedTask;
});
await Test("Account creation still requires completed onboarding", async () => {
    var f = new Fixture();
    var creator = new AccountCreator(new CreateAccountValidator(), f.Accounts, f.Clock);
    var handler = new CreateAccountHandler(f.User, f.Households, creator, f.Work);
    var command = new CreateAccountCommand(f.Household.Id, "New", AccountType.Checking, AccountScope.Household,
        [f.Member.Id], AccountVisibility.Household, true, "USD", 0, new(2026, 9, 1));
    await Throws<OnboardingRequiredException>(() => handler.HandleAsync(command, default));
    f.Household.CompleteOnboarding("Home", "HU", CurrencyCode.From("HUF"), f.Now);
    f.Household.CompleteFinancialSetup(f.Now);
    await handler.HandleAsync(command, default);
    Check(f.Accounts.Added?.Currency.Value == "USD" && f.Work.Saves == 1);
});
await Test("Currency amount validation retains exact four decimal precision", async () => {
    var f = new Fixture();
    var validator = new CreateAccountValidator();
    var c = new CreateAccountCommand(f.Household.Id, "Name", AccountType.Checking, AccountScope.Personal,
        [f.Member.Id], AccountVisibility.Private, false, "EUR", 999999999999999.9999m, new(2026, 9, 1));
    Check((await validator.ValidateAsync(c)).IsValid);
    Check(!(await validator.ValidateAsync(c with { BalanceAnchorAmount = 0.00001m })).IsValid);
    Check(!(await validator.ValidateAsync(c with { Currency = "GBP" })).IsValid);
});
await Test("Update contract requires a version and has no immutable financial fields", () => {
    var names = typeof(UpdateAccountRequest).GetProperties().Select(p => p.Name).ToHashSet();
    Check(!names.Contains("Currency") && !names.Contains("BalanceAnchorAmount") && !names.Contains("Scope"));
    try { JsonSerializer.Deserialize<UpdateAccountRequest>("{}"); throw new Exception("Expected JsonRequired failure"); }
    catch (JsonException) { }
    return Task.CompletedTask;
});
await Test("EF model configures concurrency and retains unchanged holder instances", () => {
    var f = new Fixture();
    using var db = new AverioDbContext(new DbContextOptionsBuilder<AverioDbContext>()
        .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test").Options);
    Check(db.Model.FindEntityType(typeof(Account))!.FindProperty(nameof(Account.UpdatedAt))!.IsConcurrencyToken);
    db.Attach(f.Account);
    var holder = f.Account.Owners.Single();
    f.Account.Update("Edited", f.Account.Type, [f.Member.Id], AccountVisibility.Private, false, f.Now.AddSeconds(2));
    db.ChangeTracker.DetectChanges();
    Check(ReferenceEquals(holder, f.Account.Owners.Single()));
    Check(db.Entry(holder).State == EntityState.Unchanged);
    Check(db.Entry(f.Account).Property(a => a.UpdatedAt).IsModified);
    return Task.CompletedTask;
});
await Test("Repeated changes advance the version at PostgreSQL precision", () => {
    var f = new Fixture();
    f.Account.Update("First", f.Account.Type, [f.Member.Id], AccountVisibility.Private, false, f.Now);
    var first = f.Account.UpdatedAt;
    f.Account.Update("Second", f.Account.Type, [f.Member.Id], AccountVisibility.Private, false, f.Now);
    Check(f.Account.UpdatedAt > first && f.Account.UpdatedAt.UtcTicks % 10 == 0);
    return Task.CompletedTask;
});
Console.WriteLine($"Passed {passed} account regression tests.");

async Task Test(string name, Func<Task> test) { await test(); passed++; Console.WriteLine($"PASS {name}"); }
static void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
static async Task Throws<T>(Func<Task> action) where T : Exception {
    try { await action(); } catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}");
}

sealed class User(Guid? userId) : ICurrentUser { public Guid? UserId => userId; }
sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
sealed class Work : IUnitOfWork {
    public int Saves { get; private set; }
    public Task<int> SaveChangesAsync(CancellationToken ct) { Saves++; return Task.FromResult(1); }
    public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken ct) => throw new NotSupportedException();
}
sealed class Households(Household value) : IHouseholdRepository {
    public void Add(Household household) => throw new NotSupportedException();
    public Task<HouseholdMember?> GetMembershipByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult(value.Members.SingleOrDefault(m => m.UserId == userId));
    public Task<Household?> GetByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult(value.Members.Any(m => m.UserId == userId) ? value : null);
    public Task<Household?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct) => Task.FromResult(id == value.Id && value.Members.Any(m => m.UserId == userId) ? value : null);
    public Task<Household?> GetByIdForUserForUpdateAsync(Guid id, Guid userId, CancellationToken ct) => GetByIdForUserAsync(id, userId, ct);
}
sealed class Accounts(Account value) : IAccountRepository {
    public Account Value { get; set; } = value;
    public Account? Added { get; private set; }
    public bool Hide { get; set; }
    public void Add(Account account) => Added = account;
    // Query privacy itself belongs to PostgreSQL integration coverage; these are handler tests.
    public Task<Account?> GetVisibleByIdAsync(Guid householdId, Guid memberId, Guid accountId, CancellationToken ct) =>
        Task.FromResult(!Hide && Value.Id == accountId && Value.HouseholdId == householdId ? Value : null);
    public Task<IReadOnlyList<Account>> GetVisibleAsync(Guid householdId, Guid memberId, bool includeArchived, CancellationToken ct) => throw new NotSupportedException();
}
sealed class Fixture {
    public DateTimeOffset Now { get; } = new(2026, 9, 19, 10, 0, 0, TimeSpan.Zero);
    public User User { get; }
    public Household Household { get; }
    public HouseholdMember Member => Household.Members.Single(member => member.UserId == User.UserId);
    public Account Account => Accounts.Value;
    public Accounts Accounts { get; }
    public Households Households { get; }
    public Work Work { get; } = new();
    public Clock Clock { get; }
    public Fixture() {
        var id = Guid.NewGuid(); User = new(id); Household = Household.CreateForUser(id, Now);
        Households = new(Household); Clock = new(Now.AddSeconds(1));
        Accounts = new(Account.Create(Household.Id, [Member.Id], "Personal", AccountType.Checking,
            AccountScope.Personal, AccountVisibility.Private, false, CurrencyCode.From("EUR"),
            123.4567m, new(2026, 9, 1), Now));
    }
    public UpdateAccountHandler Update(ICurrentUser? user = null) => new(user ?? User, Households, Accounts, new CreateAccountValidator(), Work, Clock);
    public UpdateAccountCommand Command() => new(Household.Id, Account.Id, "Edited", Account.Type,
        [Member.Id], Account.Visibility, Account.IncludeInHouseholdReports, Account.UpdatedAt);
}
