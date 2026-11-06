namespace Averio.Domain.Imports;

public sealed class ImportBatch
{
    private ImportBatch() { }
    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid MemberId { get; private set; }
    public string Fingerprint { get; private set; } = "";
    public string Payload { get; private set; } = "";
    public string? SourceName { get; private set; }
    public string? RecordLinks { get; private set; }
    public string? Summary { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public static ImportBatch Create(Guid householdId, Guid memberId, string fingerprint, string payload, DateTimeOffset now, string? sourceName = null)
        => new() { Id = Guid.NewGuid(), HouseholdId = householdId, MemberId = memberId,
            Fingerprint = fingerprint, Payload = payload, CreatedAt = now, SourceName = sourceName };
    public void Complete(DateTimeOffset now, string? recordLinks = null, string? summary = null)
    {
        if (CompletedAt is not null) throw new InvalidOperationException();
        RecordLinks = recordLinks;
        Summary = summary;
        CompletedAt = now;
    }
}

