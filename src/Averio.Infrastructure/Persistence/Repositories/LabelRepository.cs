using Averio.Application.Common.Abstractions;
using Averio.Domain.Labels;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class LabelRepository(AverioDbContext dbContext)
    : ILabelRepository
{
    public void Add(Label label)
        => dbContext.Labels.Add(label);

    public async Task<IReadOnlyList<Label>> GetApplicableAsync(
        Guid householdId,
        Guid? accountId,
        CancellationToken cancellationToken)
        => await dbContext.Labels
            .AsNoTracking()
            .Where(label =>
                label.HouseholdId == householdId
                && (
                    label.Scope == LabelScope.Household
                    || (accountId != null
                        && label.Scope == LabelScope.Account
                        && label.AccountId == accountId)
                ))
            .OrderBy(label => label.Scope)
            .ThenBy(label => label.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Label>> GetApplicableByIdsAsync(
        Guid householdId,
        Guid accountId,
        IReadOnlyCollection<Guid> labelIds,
        CancellationToken cancellationToken)
        => await dbContext.Labels
            .AsNoTracking()
            .Where(label =>
                labelIds.Contains(label.Id)
                && label.HouseholdId == householdId
                && (
                    label.Scope == LabelScope.Household
                    || (label.Scope == LabelScope.Account
                        && label.AccountId == accountId)
                ))
            .ToListAsync(cancellationToken);

    public Task<Label?> FindByNormalizedNameAsync(
        Guid householdId,
        Guid? accountId,
        LabelScope scope,
        string normalizedName,
        CancellationToken cancellationToken)
        => dbContext.Labels
            .AsNoTracking()
            .SingleOrDefaultAsync(
                label =>
                    label.HouseholdId == householdId
                    && label.Scope == scope
                    && label.AccountId == accountId
                    && label.NormalizedName == normalizedName,
                cancellationToken);
}
