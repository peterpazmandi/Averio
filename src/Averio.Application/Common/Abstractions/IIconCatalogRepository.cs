using Averio.Domain.Categories;

namespace Averio.Application.Common.Abstractions;

public interface IIconCatalogRepository
{
    Task<IReadOnlyList<IconCatalogEntry>> GetAllAsync(
        CancellationToken cancellationToken);
}
