namespace Averio.Contracts.Categories;

public sealed record CategoryResponse(
    Guid Id,
    Guid? ParentId,
    string? SystemKey,
    string? Name,
    string Kind,
    string Icon,
    string AccentColor,
    int SortOrder,
    bool IsSystem);

public sealed record IconResponse(
    int Id,
    string Name,
    string Value,
    int SortOrder);
