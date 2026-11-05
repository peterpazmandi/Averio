using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Common;
using Averio.Domain.Accounts;
using Averio.Domain.Imports;
using Averio.Domain.Transactions;

namespace Averio.Application.Imports;

public sealed record ImportMappingDefaults(IReadOnlyList<ImportAccountMapping> Accounts, IReadOnlyList<ImportCategoryMapping> Categories);
public sealed record ImportExistingRecord(Guid Id, string Kind, Guid AccountId, Guid? DestinationId, DateOnly Date,
    decimal Amount, decimal? ReceivedAmount, string Description, DateTimeOffset Version);
public sealed record ImportOverlap(int RowIndex, IReadOnlyList<ImportOverlapMatch> Matches, int MatchCount);
public sealed record ImportOverlapMatch(Guid Id, string Kind, string Description, DateOnly Date, string Amount);
public sealed record ImportReviewRequest(IReadOnlyList<ImportAccountMapping> Accounts);
public sealed record ImportReview(string Token, IReadOnlyList<ImportOverlap> Overlaps);
public sealed record ImportBalance(Guid AccountId, string Name, string Currency, DateOnly AnchorDate, string AnchorAmount, string CurrentBalance, DateOnly AsOf);
public sealed record ImportUndoRecord(Guid Id, bool IsTransfer, string State);
public sealed record ImportUndoPlan(string Token, IReadOnlyList<ImportUndoRecord> Records, int Eligible, int Retained, IReadOnlyList<ImportBalanceEffect> Effects);
public sealed record ImportBalanceEffect(Guid AccountId, string Name, string Currency, string Before, string After, string Change);
public sealed record ImportUndoRequest(string Token, bool Confirm);
public sealed record ImportUndoResult(int Voided, int Retained);

public sealed partial class ImportHandler
{
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static ImportRecordLink[] Links(ImportBatch batch) => batch.RecordLinks is null ? [] : JsonSerializer.Deserialize<ImportRecordLink[]>(batch.RecordLinks)!;
    private static string Amount(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private async Task<ImportMappingDefaults> DefaultsAsync(Guid householdId, Guid memberId, CancellationToken ct)
    {
        var accountMappings = new Dictionary<string, ImportAccountMapping>();
        var categoryMappings = new Dictionary<(string, string), ImportCategoryMapping>();
        foreach (var batch in await imports.RecentMappedAsync(householdId, memberId, ct))
        {
            var rows = Rows(batch); var links = Links(batch);
            if (rows.Length != links.Length) continue;
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i]; var link = links[i];
                if (link.AccountId is Guid a) accountMappings.TryAdd(row.Account, new(row.Account, a, link.Visibility ?? "Private"));
                if (link.DestinationId is Guid d && row.Destination is not null) accountMappings.TryAdd(row.Destination, new(row.Destination, d, "Private"));
                if (link.CategoryId is Guid c) categoryMappings.TryAdd((row.Kind, row.Category), new(row.Kind, row.Category, c));
            }
        }
        // Previously selected identifiers are suggestions only, and must still be accessible now.
        var validAccounts = new List<ImportAccountMapping>();
        foreach (var mapping in accountMappings.Values)
        {
            var account = await accounts.GetVisibleByIdAsync(householdId, memberId, mapping.AccountId, ct);
            if (account is null || account.Status != AccountStatus.Active || account.CutoverAtUtc != null
                || (account.Scope != AccountScope.Household && !account.Owners.Any(o => o.HouseholdMemberId == memberId))) continue;
            validAccounts.Add(mapping with { Visibility = account.Scope == AccountScope.Household ? "Household" : account.Visibility == AccountVisibility.Private ? "Private" : mapping.Visibility });
        }
        var available = await categories.GetAvailableByIdsAsync(householdId, categoryMappings.Values.Select(c => c.CategoryId!.Value).Distinct().ToArray(), ct);
        return new(validAccounts, categoryMappings.Values.Where(c => available.Any(a => a.Id == c.CategoryId && !a.IsArchived && a.Supports(Enum.Parse<TransactionKind>(c.Kind)))).ToArray());
    }

    public async Task<ImportReview> ReviewAsync(Guid householdId, Guid id, ImportReviewRequest request, CancellationToken ct)
    {
        var member = await MemberAsync(householdId, ct);
        var batch = await imports.GetAsync(householdId, member.Id, id, ct) ?? throw new NotFoundException("import", id);
        return await ReviewCoreAsync(householdId, member.Id, batch, request.Accounts, ct);
    }
    private async Task<ImportReview> ReviewCoreAsync(Guid householdId, Guid memberId, ImportBatch batch, IReadOnlyList<ImportAccountMapping> mappings, CancellationToken ct)
    {
        var rows = Rows(batch);
        var sources = rows.SelectMany(r => r.Destination is null ? new[] { r.Account } : new[] { r.Account, r.Destination }).Distinct().ToArray();
        if (mappings is null || mappings.Count != sources.Length || mappings.Any(m => m is null || !sources.Contains(m.Source))
            || mappings.Select(m => m.Source).Distinct().Count() != sources.Length || mappings.Select(m => m.AccountId).Distinct().Count() != sources.Length) throw MappingError();
        foreach (var mapping in mappings)
        {
            var account = await accounts.GetVisibleByIdAsync(householdId, memberId, mapping.AccountId, ct) ?? throw new NotFoundException("account", mapping.AccountId);
            if (account.Status != AccountStatus.Active || account.CutoverAtUtc != null
                || (account.Scope != AccountScope.Household && !account.Owners.Any(o => o.HouseholdMemberId == memberId))) throw new ForbiddenException();
        }
        var map = mappings.ToDictionary(m => m.Source, m => m.AccountId);
        var candidates = await imports.FindOverlapAsync(householdId, memberId, map.Values.ToArray(), rows.Min(r => r.Date), rows.Max(r => r.Date), ct);
        var lookup = candidates.ToLookup(c => (c.AccountId, c.Kind, c.Date, c.Amount, c.DestinationId));
        var overlaps = rows.Select((r, i) => {
            var matches = lookup[(map[r.Account], r.Kind, r.Date, r.Amount, r.Destination is null ? null : (Guid?)map[r.Destination])]
                .Where(c => r.Kind != "Transfer" || c.ReceivedAmount == (r.ReceivedAmount ?? r.Amount)).OrderBy(c => c.Id).ToArray();
            return new ImportOverlap(i, matches.Take(3).Select(c => new ImportOverlapMatch(c.Id, c.Kind, c.Description, c.Date, Amount(c.Amount))).ToArray(), matches.Length);
        }).Where(o => o.MatchCount > 0).ToArray();
        return new(Hash(new { batch.Id, Mappings = mappings.OrderBy(m => m.Source).ToArray(), Candidates = candidates.OrderBy(c => c.Id).ToArray() }), overlaps);
    }

    public async Task<IReadOnlyList<ImportBalance>> BalancesAsync(Guid householdId, Guid id, DateOnly? asOf, CancellationToken ct)
    {
        var member = await MemberAsync(householdId, ct);
        var batch = await imports.GetAsync(householdId, member.Id, id, ct) ?? throw new NotFoundException("import", id);
        if (batch.CompletedAt is null) throw new NotFoundException("import", id);
        var day = asOf ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var result = new List<ImportBalance>();
        var storedLinks = Links(batch);
        var liveRecords = await imports.GetCurrentRecordsAsync(householdId, member.Id,
            storedLinks.Where(l => l.Kind != "Transfer").Select(l => l.Id).ToArray(), storedLinks.Where(l => l.Kind == "Transfer").Select(l => l.Id).ToArray(), ct);
        var balanceLinks = storedLinks.Select(l => liveRecords.FirstOrDefault(c => c.Id == l.Id) is { } live ? l with { AccountId = live.AccountId, DestinationId = live.DestinationId } : l);
        foreach (var accountId in balanceLinks.SelectMany(l => new[] { l.AccountId, l.DestinationId }).OfType<Guid>().Distinct())
        {
            var account = await accounts.GetVisibleByIdAsync(householdId, member.Id, accountId, ct);
            if (account is null) continue;
            var current = await balances.GetCurrentBalancesAsync(householdId, member.Id, [accountId], day, ct);
            result.Add(new(accountId, account.Name, account.Currency.Value, account.BalanceAnchorDate, Amount(account.BalanceAnchorAmount), Amount(current[accountId]), day));
        }
        return result;
    }

    public async Task<ImportUndoPlan> UndoPlanAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        // Keep the preview internally consistent with writes, using the same household lock as confirmation.
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        var member = await MemberAsync(householdId, ct);
        var batch = await imports.GetAsync(householdId, member.Id, id, ct) ?? throw new NotFoundException("import", id);
        var plan = await UndoPlanCoreAsync(householdId, member.Id, batch, ct);
        await tx.CommitAsync(ct);
        return plan;
    }
    private async Task<ImportUndoPlan> UndoPlanCoreAsync(Guid householdId, Guid memberId, ImportBatch batch, CancellationToken ct)
    {
        if (batch.CompletedAt is null) throw new NotFoundException("import", batch.Id);
        var rows = Rows(batch); var links = Links(batch);
        if (links.Length != rows.Length) return new(Hash(batch.Id), [], 0, rows.Length, []);
        var current = (await imports.GetCurrentRecordsAsync(householdId, memberId,
            links.Where(l => l.Kind != "Transfer").Select(l => l.Id).ToArray(), links.Where(l => l.Kind == "Transfer").Select(l => l.Id).ToArray(), ct)).ToDictionary(r => r.Id);
        links = links.Select(l => current.TryGetValue(l.Id, out var live) ? l with { AccountId = live.AccountId, DestinationId = live.DestinationId } : l).ToArray();
        var linked = await imports.LinkedRecordsAsync(householdId, links.Select(l => l.Id).ToArray(), ct);
        var records = links.Select(l => {
            var live = current.GetValueOrDefault(l.Id);
            return new ImportUndoRecord(live?.Id ?? Guid.Empty, l.Kind == "Transfer", l.Skipped ? "Skipped" : live is null ? "Unavailable" : live.Voided ? "Voided" :
                StoredVersion(live.Version) != l.Version ? "Edited" : !live.CanModify ? "ReadOnly" : linked.Contains(l.Id) ? "Linked" :
                l.AccountId is null || (l.Kind == "Transfer" && l.DestinationId is null) ? "Untracked" : "Eligible");
        }).ToArray();
        var effects = new Dictionary<Guid, decimal>();
        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var accountCache = new Dictionary<Guid, Account>();
        async Task Leg(Guid accountId, decimal change, DateOnly date)
        {
            if (!accountCache.TryGetValue(accountId, out var a)) {
                a = await accounts.GetVisibleByIdAsync(householdId, memberId, accountId, ct) ?? throw new NotFoundException("account", accountId); accountCache.Add(accountId, a);
            }
            effects.TryAdd(accountId, 0);
            if (date >= a.BalanceAnchorDate && date <= day) effects[accountId] += change;
        }
        for (int i = 0; i < rows.Length; i++) if (records[i].State == "Eligible")
        {
            var r = rows[i]; var l = links[i];
            await Leg(l.AccountId!.Value, r.Kind == "Income" ? -r.Amount : r.Amount, r.Date);
            if (r.Kind == "Transfer") await Leg(l.DestinationId!.Value, -(r.ReceivedAmount ?? r.Amount), r.Date);
        }
        var before = await balances.GetCurrentBalancesAsync(householdId, memberId, effects.Keys.ToArray(), day, ct);
        var balanceEffects = effects.OrderBy(e => e.Key).Select(e => new ImportBalanceEffect(e.Key, accountCache[e.Key].Name, accountCache[e.Key].Currency.Value,
            Amount(before[e.Key]), Amount(before[e.Key] + e.Value), Amount(e.Value))).ToArray();
        var token = Hash(new { batch.Id, Records = records, Current = current.Values.OrderBy(r => r.Id).ToArray(), Effects = balanceEffects });
        return new(token, records, records.Count(r => r.State == "Eligible"), records.Count(r => r.State != "Eligible"), balanceEffects);
    }
    public async Task<ImportUndoResult> UndoAsync(Guid householdId, Guid id, ImportUndoRequest request, CancellationToken ct)
    {
        if (!request.Confirm) throw MappingError();
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        var member = await MemberAsync(householdId, ct);
        var batch = await imports.GetAsync(householdId, member.Id, id, ct) ?? throw new NotFoundException("import", id);
        var plan = await UndoPlanCoreAsync(householdId, member.Id, batch, ct);
        if (plan.Token != request.Token) throw new ConflictException("import_review_changed", "Error_ImportReviewChanged");
        var eligible = plan.Records.Where(r => r.State == "Eligible").ToArray();
        await imports.VoidRecordsAsync(householdId, eligible.Where(r => !r.IsTransfer).Select(r => r.Id).ToArray(),
            eligible.Where(r => r.IsTransfer).Select(r => r.Id).ToArray(), clock.GetUtcNow(), ct);
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(eligible.Length, plan.Retained);
    }
}