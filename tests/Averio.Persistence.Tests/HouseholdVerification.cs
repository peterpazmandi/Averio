using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Households.GetHousehold;
using Averio.Application.Households.UpdateHousehold;
using Averio.Application.Onboarding.CompleteOnboarding;
using Averio.Application.Onboarding.SaveOnboardingHousehold;
using Averio.Domain.Common;
using Averio.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class HouseholdVerification
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    public static async Task RunAsync(IServiceProvider provider, MutableCurrentUser user, Fixture f)
    {
        async Task<HouseholdResult> Read(Guid? id = null)
        {
            await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<GetHouseholdHandler>().HandleAsync(id ?? f.HouseholdId, Ct);
        }
        async Task Update(UpdateHouseholdCommand command)
        {
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<UpdateHouseholdHandler>().HandleAsync(command, Ct);
        }

        user.UserId = f.OwnerUserId;
        var original = await Read();
        Check(original.Version != Guid.Empty && original.Role == "owner", "Owner/version projection");
        Check(original.Members.Count == 2, "Directory member count");
        Check(original.Members.Single(m => m.IsCurrentUser).Id == f.OwnerMemberId, "Owner self marker");
        Check(original.Members.Single(m => m.Id == f.MemberId).DisplayName == "Member", "Member display name");
        Check(typeof(HouseholdMemberResult).GetProperties().Select(p => p.Name).Order().SequenceEqual(
            new[] { "DisplayName", "Id", "IsCurrentUser", "JoinedAtUtc", "Role" }), "Directory must expose only approved metadata");

        var command = new UpdateHouseholdCommand(f.HouseholdId, "  Our household  ", "hu", "HUF", original.Version);
        user.UserId = f.MemberUserId;
        var member = await Read();
        Check(member.Role == "member" && member.Members.Single(m => m.IsCurrentUser).Id == f.MemberId, "Member self marker");
        await Throws<ForbiddenException>(() => Update(command));
        user.UserId = Guid.NewGuid();
        await Throws<NotFoundException>(() => Read());
        await Throws<NotFoundException>(() => Update(command));
        user.UserId = null;
        await Throws<AuthenticationFailedException>(() => Read());
        await Throws<AuthenticationFailedException>(() => Update(command));

        user.UserId = f.OwnerUserId;
        await Throws<NotFoundException>(() => Read(Guid.NewGuid()));
        await Throws<ValidationException>(() => Update(command with { ExpectedVersion = Guid.Empty }));
        await Throws<ValidationException>(() => Update(command with { Name = " " }));
        await Throws<ValidationException>(() => Update(command with { CountryCode = "Hungary" }));
        await Throws<ValidationException>(() => Update(command with { PreferredCurrency = "GBP" }));
        await Update(command);
        var changed = await Read();
        Check(changed.Name == "Our household" && changed.CountryCode == "HU" && changed.PreferredCurrency == "HUF", "Normalized profile");
        Check(changed.Version != original.Version, "Version advances");
        await Throws<ConflictException>(() => Update(command with { Name = "Stale overwrite" }));
        Check((await Read()).Name == changed.Name, "Stale request preserved profile");

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            Check((await db.Accounts.SingleAsync(a => a.Id == f.HouseholdAccountId)).Currency == CurrencyCode.Eur,
                "Reporting preference must not change account currency");
            await Throws<ConflictException>(() => scope.ServiceProvider.GetRequiredService<SaveOnboardingHouseholdHandler>()
                .HandleAsync(new("Bypass settings", "DE", "EUR"), Ct));
            await Throws<ConflictException>(() => scope.ServiceProvider.GetRequiredService<CompleteOnboardingHandler>()
                .HandleAsync(new("Owner", "Bypass settings", "DE", "EUR"), Ct));
        }

        // Both writers load the same version before the first commit. Exercise the
        // database concurrency token and UnitOfWork mapping, not just request checks.
        await using (var firstScope = provider.CreateAsyncScope())
        await using (var secondScope = provider.CreateAsyncScope())
        {
            var firstDb = firstScope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var secondDb = secondScope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var first = await firstDb.Households.SingleAsync(h => h.Id == f.HouseholdId);
            var second = await secondDb.Households.SingleAsync(h => h.Id == f.HouseholdId);
            first.UpdateProfile("First writer", "HU", CurrencyCode.Huf);
            second.UpdateProfile("Lost writer", "DE", CurrencyCode.Eur);
            await firstScope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
            await Throws<ConflictException>(() => secondScope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct));
        }
        Check((await Read()).Name == "First writer", "Database race must preserve winner");
        // Restore fixture preferences for the other suites.
        var latest = await Read();
        await Update(new(f.HouseholdId, original.Name, original.CountryCode, original.PreferredCurrency, latest.Version));
        Console.WriteLine("PASS household directory, owner permissions and settings concurrency");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static async Task Throws<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}");
    }
}
