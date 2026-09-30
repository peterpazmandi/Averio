using Averio.Domain.Common;

namespace Averio.Domain.Transfers;

// One aggregate owns both movements: a leg can never be posted or voided alone.
public sealed class Transfer
{
    private Transfer() { }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid SourceAccountId { get; private set; }
    public Guid DestinationAccountId { get; private set; }
    public Guid CreatedByHouseholdMemberId { get; private set; }
    public CurrencyCode SourceCurrency { get; private set; }
    public CurrencyCode DestinationCurrency { get; private set; }
    public decimal SentAmount { get; private set; }
    public decimal ReceivedAmount { get; private set; }
    public DateOnly OccurredOn { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? VoidedAt { get; private set; }

    public static Transfer Create(Guid householdId, Guid sourceAccountId,
        Guid destinationAccountId, Guid createdByHouseholdMemberId,
        CurrencyCode sourceCurrency, CurrencyCode destinationCurrency,
        decimal sentAmount, decimal receivedAmount, DateOnly occurredOn,
        string? note, DateTimeOffset now)
    {
        if (householdId == Guid.Empty || createdByHouseholdMemberId == Guid.Empty
            || sourceAccountId == Guid.Empty || destinationAccountId == Guid.Empty
            || sourceAccountId == destinationAccountId)
            throw new ArgumentException("A transfer requires a household, creator and two distinct accounts.");
        if (!CurrencyCode.IsSupported(sourceCurrency.Value)
            || !CurrencyCode.IsSupported(destinationCurrency.Value))
            throw new ArgumentException("Unsupported currency.");

        var transfer = new Transfer
        {
            Id = Guid.NewGuid(), HouseholdId = householdId,
            SourceAccountId = sourceAccountId, DestinationAccountId = destinationAccountId,
            CreatedByHouseholdMemberId = createdByHouseholdMemberId,
            SourceCurrency = sourceCurrency, DestinationCurrency = destinationCurrency,
            CreatedAt = Truncate(now), UpdatedAt = Truncate(now)
        };
        transfer.SetDetails(sentAmount, receivedAmount, occurredOn, note);
        return transfer;
    }

    // Accounts/currencies are immutable. Correct a wrong pair by voiding and recreating.
    public void Update(decimal sentAmount, decimal receivedAmount, DateOnly occurredOn,
        string? note, DateTimeOffset now)
    {
        if (VoidedAt is not null)
            throw new InvalidOperationException("Voided transfers cannot be edited.");
        SetDetails(sentAmount, receivedAmount, occurredOn, note);
        UpdatedAt = NextVersion(now);
    }

    public void Void(DateTimeOffset now)
    {
        if (VoidedAt is not null) return;
        UpdatedAt = NextVersion(now);
        VoidedAt = UpdatedAt;
    }

    public static bool IsValidAmount(decimal amount)
        => amount > 0 && amount <= 999999999999999.9999m
            && decimal.Round(amount, 4) == amount;

    private void SetDetails(decimal sent, decimal received, DateOnly date, string? note)
    {
        if (!IsValidAmount(sent) || !IsValidAmount(received))
            throw new ArgumentOutOfRangeException(nameof(sent));
        if (SourceCurrency == DestinationCurrency && sent != received)
            throw new ArgumentException("Same-currency transfer amounts must match.");
        if (date == default) throw new ArgumentException("Transfer date is required.");
        if (note?.Trim().Length > 500) throw new ArgumentOutOfRangeException(nameof(note));
        SentAmount = sent;
        ReceivedAmount = received;
        OccurredOn = date;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    private static DateTimeOffset Truncate(DateTimeOffset now)
        => new(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);

    private DateTimeOffset NextVersion(DateTimeOffset now)
        => new(Math.Max(Truncate(now).UtcTicks, UpdatedAt.UtcTicks + 10), TimeSpan.Zero);
}
