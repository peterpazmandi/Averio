using System.Globalization;
using System.Text.Json;
using Averio.Application.Common.Exceptions;

namespace Averio.Application.Imports;

public sealed partial class ImportHandler
{
    private static DateTimeOffset StoredVersion(DateTimeOffset value)
        => new(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);
    private static string? CleanSourceName(string? name)
        => name is null ? null : new string(name.Replace('\\', '/').Split('/').Last().Where(c => !char.IsControl(c)).Take(200).ToArray());
    private static ImportHistorySummary Summarize(IReadOnlyList<ImportRow> rows)
        => new(rows.Count, rows.Min(r => r.Date), rows.Max(r => r.Date),
            rows.GroupBy(r => (r.Kind, r.Currency)).Select(g => new ImportTotal(g.Key.Kind, g.Key.Currency,
                g.Sum(r => r.Amount).ToString(CultureInfo.InvariantCulture), g.Count())).ToArray());
    public async Task<ImportHistoryPage> HistoryAsync(Guid householdId, int page, CancellationToken ct)
    {
        var member = await MemberAsync(householdId, ct);
        var batches = await imports.ListCompletedAsync(householdId, member.Id, Math.Clamp(page, 0, 100000), ct);
        return new(batches.Take(10).Select(b => new ImportHistoryItem(b.Id, b.SourceName, b.CompletedAt,
            b.Summary is null ? Summarize(JsonSerializer.Deserialize<ImportRow[]>(b.LegacyPayload!)!)
                : JsonSerializer.Deserialize<ImportHistorySummary>(b.Summary)!, b.TrackingAvailable)).ToArray(), batches.Count > 10);
    }
    public async Task<ImportHistoryDetail> HistoryDetailAsync(Guid householdId, Guid id, int page, CancellationToken ct)
    {
        var member = await MemberAsync(householdId, ct);
        var batch = await imports.GetAsync(householdId, member.Id, id, ct) ?? throw new NotFoundException("import", id);
        if (batch.CompletedAt is null) throw new NotFoundException("import", id);
        var rows = Rows(batch);
        var links = batch.RecordLinks is null ? [] : JsonSerializer.Deserialize<ImportRecordLink[]>(batch.RecordLinks)!;
        var tracking = links.Length == rows.Length;
        var current = tracking ? (await imports.GetCurrentRecordsAsync(householdId, member.Id,
            links.Where(l => l.Kind != "Transfer").Select(l => l.Id).ToArray(),
            links.Where(l => l.Kind == "Transfer").Select(l => l.Id).ToArray(), ct))
                .ToDictionary(r => (r.IsTransfer, r.Id)) : [];
        var records = rows.Select((r, index) =>
        {
            var link = tracking ? links[index] : null;
            var live = link is null ? null : current.GetValueOrDefault((link.Kind == "Transfer", link.Id));
            bool? changed = live is null ? null : StoredVersion(live.Version) != link!.Version;
            var state = !tracking ? "Untracked" : live is null ? "Unavailable" : live.Voided ? "Voided" : changed == true ? "Edited" : "Unchanged";
            return new ImportHistoryRecord(new(r.Sheet, r.RowNumber, r.Kind, r.Date, r.Account, r.Destination, r.Category,
                r.Amount.ToString(CultureInfo.InvariantCulture), r.Currency, r.ReceivedAmount?.ToString(CultureInfo.InvariantCulture),
                r.ReceivedCurrency, r.Tags, r.Note), live?.Id, state, changed, live?.CanModify ?? false,
                link?.Account, link?.Destination, link?.Category, link?.CategoryIsBase ?? false);
        }).ToArray();
        var offset = Math.Clamp(page, 0, 100000) * 50;
        return new(new(batch.Id, batch.SourceName, batch.CompletedAt.Value, Summarize(rows), tracking),
            records.Skip(offset).Take(50).ToArray(),
            new(records.Count(r => r.State == "Unchanged"), records.Count(r => r.State == "Edited"),
                records.Count(r => r.State == "Voided"), records.Count(r => r.State == "Unavailable"),
                records.Count(r => r.State == "Untracked"), records.Count(r => r.RecordId is not null && !r.CanModify)),
            clock.GetUtcNow(), records.Length > offset + 50);
    }
}
