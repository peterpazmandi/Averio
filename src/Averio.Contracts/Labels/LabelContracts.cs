namespace Averio.Contracts.Labels;

public sealed record LabelResponse(
    Guid Id,
    string Name,
    string Scope,
    Guid? AccountId);

public sealed record CreateLabelRequest(
    string Name,
    string Scope,
    Guid? AccountId);

public sealed record CreateLabelResponse(Guid Id);
