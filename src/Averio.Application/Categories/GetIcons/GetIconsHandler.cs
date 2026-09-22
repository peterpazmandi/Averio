using Averio.Application.Common.Abstractions;

namespace Averio.Application.Categories.GetIcons;

public sealed class GetIconsHandler(IIconCatalogRepository repository)
{
    public async Task<IReadOnlyList<IconResult>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var icons = await repository.GetAllAsync(cancellationToken);

        return icons
            .Select(icon => new IconResult(
                icon.Id,
                icon.Name,
                icon.Value,
                icon.SortOrder))
            .ToArray();
    }
}

public sealed record IconResult(
    int Id,
    string Name,
    string Value,
    int SortOrder);
