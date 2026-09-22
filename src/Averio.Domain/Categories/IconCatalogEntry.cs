namespace Averio.Domain.Categories;

public sealed class IconCatalogEntry
{
    private IconCatalogEntry() { }

    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Value { get; private set; } = null!;
    public int SortOrder { get; private set; }
}
