using Averio.Domain.Common;

namespace Averio.Domain.Transactions;

public sealed class Transaction
{
    private Transaction() { }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid CreatedByHouseholdMemberId { get; private set; }
    public TransactionKind Kind { get; private set; }
    public TransactionVisibility Visibility { get; private set; }
    public decimal Amount { get; private set; }
    public CurrencyCode Currency { get; private set; }
    public string Description { get; private set; } = null!;
    public string? Note { get; private set; }
    public DateOnly OccurredOn { get; private set; }
    public TransactionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? VoidedAt { get; private set; }

    public static Transaction Create(
        Guid householdId,
        Guid accountId,
        Guid createdByHouseholdMemberId,
        TransactionKind kind,
        TransactionVisibility visibility,
        decimal amount,
        CurrencyCode currency,
        string description,
        string? note,
        DateOnly occurredOn,
        DateTimeOffset now)
    {
        Validate(
            householdId,
            accountId,
            createdByHouseholdMemberId,
            kind,
            visibility,
            amount,
            currency,
            description,
            note,
            occurredOn);

        return new Transaction
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            AccountId = accountId,
            CreatedByHouseholdMemberId = createdByHouseholdMemberId,
            Kind = kind,
            Visibility = visibility,
            Amount = amount,
            Currency = currency,
            Description = description.Trim(),
            Note = NormalizeNote(note),
            OccurredOn = occurredOn,
            Status = TransactionStatus.Posted,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Update(
        Guid accountId,
        TransactionKind kind,
        TransactionVisibility visibility,
        decimal amount,
        CurrencyCode currency,
        string description,
        string? note,
        DateOnly occurredOn,
        DateTimeOffset now)
    {
        if (Status != TransactionStatus.Posted)
        {
            throw new InvalidOperationException("Voided transactions cannot be edited.");
        }

        Validate(
            HouseholdId,
            accountId,
            CreatedByHouseholdMemberId,
            kind,
            visibility,
            amount,
            currency,
            description,
            note,
            occurredOn);

        AccountId = accountId;
        Kind = kind;
        Visibility = visibility;
        Amount = amount;
        Currency = currency;
        Description = description.Trim();
        Note = NormalizeNote(note);
        OccurredOn = occurredOn;
        UpdatedAt = NextVersion(now);
    }

    public void Void(DateTimeOffset now)
    {
        if (Status == TransactionStatus.Voided)
        {
            return;
        }

        Status = TransactionStatus.Voided;
        VoidedAt = now;
        UpdatedAt = NextVersion(now);
    }

    private static void Validate(
        Guid householdId,
        Guid accountId,
        Guid createdByHouseholdMemberId,
        TransactionKind kind,
        TransactionVisibility visibility,
        decimal amount,
        CurrencyCode currency,
        string description,
        string? note,
        DateOnly occurredOn)
    {
        if (householdId == Guid.Empty)
            throw new ArgumentException("Household identifier is required.", nameof(householdId));
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account identifier is required.", nameof(accountId));
        if (createdByHouseholdMemberId == Guid.Empty)
            throw new ArgumentException("Creator membership is required.", nameof(createdByHouseholdMemberId));
        if (!Enum.IsDefined(kind))
            throw new ArgumentException("Invalid transaction kind.", nameof(kind));
        if (!Enum.IsDefined(visibility))
            throw new ArgumentException("Invalid transaction visibility.", nameof(visibility));
        if (amount <= 0 || amount > 999999999999999.9999m || decimal.Round(amount, 4) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (!CurrencyCode.IsSupported(currency.Value))
            throw new ArgumentException("Unsupported currency.", nameof(currency));
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 160)
            throw new ArgumentOutOfRangeException(nameof(description));
        if (note?.Trim().Length > 500)
            throw new ArgumentOutOfRangeException(nameof(note));
        if (occurredOn == default)
            throw new ArgumentException("Transaction date is required.", nameof(occurredOn));
    }

    private static string? NormalizeNote(string? note)
        => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    private DateTimeOffset NextVersion(DateTimeOffset now) => new(
        Math.Max(
            now.UtcTicks - now.UtcTicks % 10,
            UpdatedAt.UtcTicks - UpdatedAt.UtcTicks % 10 + 10),
        TimeSpan.Zero);
}
