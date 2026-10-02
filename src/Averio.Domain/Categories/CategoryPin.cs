namespace Averio.Domain.Categories;

// A preference owned by a household membership, never a property of the shared category.
public sealed class CategoryPin
{
    private CategoryPin() { }
    public Guid HouseholdMemberId { get; private set; }
    public Guid CategoryId { get; private set; }
    public int SortOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
