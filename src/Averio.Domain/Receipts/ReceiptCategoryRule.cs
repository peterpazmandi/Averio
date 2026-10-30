namespace Averio.Domain.Receipts;

public sealed class ReceiptCategoryRule
{
    public Guid HouseholdId { get; private set; }
    public string MatchKey { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public Guid UpdatedByMemberId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}
