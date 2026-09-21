using Averio.Domain.Common;

namespace Averio.Domain.Transactions;

public sealed class Transaction
{
    private readonly List<TransactionItem> _items = [];
    private readonly List<TransactionLabel> _labels = [];

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
    public string? Merchant { get; private set; }
    public string? Note { get; private set; }
    public DateOnly OccurredOn { get; private set; }
    public TransactionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? VoidedAt { get; private set; }
    public IReadOnlyCollection<TransactionItem> Items => _items;
    public IReadOnlyCollection<TransactionLabel> Labels => _labels;

    public static Transaction Create(
        Guid householdId,
        Guid accountId,
        Guid createdByHouseholdMemberId,
        TransactionKind kind,
        TransactionVisibility visibility,
        CurrencyCode currency,
        string? merchant,
        string? note,
        DateOnly occurredOn,
        IReadOnlyCollection<TransactionItemDraft> items,
        IReadOnlyCollection<Guid> labelIds,
        DateTimeOffset now)
    {
        ValidateBase(
            householdId,
            accountId,
            createdByHouseholdMemberId,
            kind,
            visibility,
            currency,
            merchant,
            note,
            occurredOn,
            items,
            labelIds);

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            AccountId = accountId,
            CreatedByHouseholdMemberId = createdByHouseholdMemberId,
            Kind = kind,
            Visibility = visibility,
            Currency = currency,
            Merchant = Normalize(merchant),
            Note = Normalize(note),
            OccurredOn = occurredOn,
            Status = TransactionStatus.Posted,
            CreatedAt = now,
            UpdatedAt = now,
        };

        transaction.ReplaceItems(items);
        transaction.ReplaceLabels(labelIds);
        transaction.Description = BuildDescription(transaction.Merchant, transaction._items);

        return transaction;
    }

    public void Update(
        Guid accountId,
        TransactionKind kind,
        TransactionVisibility visibility,
        CurrencyCode currency,
        string? merchant,
        string? note,
        DateOnly occurredOn,
        IReadOnlyCollection<TransactionItemDraft> items,
        IReadOnlyCollection<Guid> labelIds,
        DateTimeOffset now)
    {
        if (Status != TransactionStatus.Posted)
            throw new InvalidOperationException("Voided transactions cannot be edited.");

        ValidateBase(
            HouseholdId,
            accountId,
            CreatedByHouseholdMemberId,
            kind,
            visibility,
            currency,
            merchant,
            note,
            occurredOn,
            items,
            labelIds);

        AccountId = accountId;
        Kind = kind;
        Visibility = visibility;
        Currency = currency;
        Merchant = Normalize(merchant);
        Note = Normalize(note);
        OccurredOn = occurredOn;

        ReplaceItems(items);
        ReplaceLabels(labelIds);
        Description = BuildDescription(Merchant, _items);
        UpdatedAt = NextVersion(now);
    }

    public void Void(DateTimeOffset now)
    {
        if (Status == TransactionStatus.Voided)
            return;

        Status = TransactionStatus.Voided;
        VoidedAt = now;
        UpdatedAt = NextVersion(now);
    }

    private void ReplaceItems(IReadOnlyCollection<TransactionItemDraft> drafts)
    {
        _items.Clear();

        var position = 0;
        foreach (var draft in drafts)
        {
            _items.Add(TransactionItem.Create(
                HouseholdId,
                Id,
                position++,
                draft));
        }

        Amount = _items.Sum(item => item.Amount);

        if (Amount <= 0 || Amount > 999999999999999.9999m)
            throw new ArgumentOutOfRangeException(nameof(drafts));
    }

    private void ReplaceLabels(IReadOnlyCollection<Guid> labelIds)
    {
        _labels.Clear();

        foreach (var labelId in labelIds.Distinct())
        {
            _labels.Add(TransactionLabel.Create(
                HouseholdId,
                Id,
                labelId));
        }
    }

    private static void ValidateBase(
        Guid householdId,
        Guid accountId,
        Guid createdByHouseholdMemberId,
        TransactionKind kind,
        TransactionVisibility visibility,
        CurrencyCode currency,
        string? merchant,
        string? note,
        DateOnly occurredOn,
        IReadOnlyCollection<TransactionItemDraft> items,
        IReadOnlyCollection<Guid> labelIds)
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
        if (!CurrencyCode.IsSupported(currency.Value))
            throw new ArgumentException("Unsupported currency.", nameof(currency));
        if (Normalize(merchant)?.Length > 160)
            throw new ArgumentOutOfRangeException(nameof(merchant));
        if (Normalize(note)?.Length > 500)
            throw new ArgumentOutOfRangeException(nameof(note));
        if (occurredOn == default)
            throw new ArgumentException("Transaction date is required.", nameof(occurredOn));
        if (items.Count is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(items));
        if (labelIds.Count > 20)
            throw new ArgumentOutOfRangeException(nameof(labelIds));
        if (labelIds.Any(id => id == Guid.Empty))
            throw new ArgumentException("Label identifier is required.", nameof(labelIds));
        if (labelIds.Distinct().Count() != labelIds.Count)
            throw new ArgumentException("Duplicate labels are not allowed.", nameof(labelIds));
    }

    private static string BuildDescription(
        string? merchant,
        IReadOnlyCollection<TransactionItem> items)
    {
        if (!string.IsNullOrWhiteSpace(merchant))
            return merchant;

        return items
            .OrderBy(item => item.Position)
            .First()
            .Description;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private DateTimeOffset NextVersion(DateTimeOffset now) => new(
        Math.Max(
            now.UtcTicks - now.UtcTicks % 10,
            UpdatedAt.UtcTicks - UpdatedAt.UtcTicks % 10 + 10),
        TimeSpan.Zero);
}
