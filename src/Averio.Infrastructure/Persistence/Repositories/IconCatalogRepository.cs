using Averio.Application.Common.Abstractions;
using Averio.Domain.Categories;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class IconCatalogRepository(AverioDbContext dbContext)
    : IIconCatalogRepository
{
    public async Task<IReadOnlyList<IconCatalogEntry>> GetAllAsync(
        CancellationToken cancellationToken)
        => await dbContext.Icons
            .AsNoTracking()
            .OrderBy(icon => icon.SortOrder)
            .ThenBy(icon => icon.Id)
            .ToListAsync(cancellationToken);
}
