using Averio.Domain.Labels;

namespace Averio.Application.Common.Abstractions;

public interface ILabelRepository
{
    void Add(Label label);

    Task<IReadOnlyList<Label>> GetApplicableAsync(
        Guid householdId,
        Guid? accountId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Label>> GetApplicableByIdsAsync(
        Guid householdId,
        Guid accountId,
        IReadOnlyCollection<Guid> labelIds,
        CancellationToken cancellationToken);

    Task<Label?> FindByNormalizedNameAsync(
        Guid householdId,
        Guid? accountId,
        LabelScope scope,
        string normalizedName,
        CancellationToken cancellationToken);
}
