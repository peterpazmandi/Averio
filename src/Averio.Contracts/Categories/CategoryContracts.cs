namespace Averio.Contracts.Categories;

public sealed record CategoryResponse(
    Guid Id, Guid? ParentId, string? SystemKey, string? Name, string Kind,
    string Icon, string AccentColor, int SortOrder, bool IsSystem,
    bool IsArchived, DateTimeOffset UpdatedAt, bool IsPinned, int? PinOrder);

public sealed record CreateCategoryRequest(
    string Name, string Kind, string Icon, string AccentColor, Guid? ParentId);

public sealed record UpdateCategoryRequest(
    string Name, string Kind, string Icon, string AccentColor, Guid? ParentId,
    DateTimeOffset ExpectedUpdatedAt);

public sealed record CategoryArchiveRequest(bool IsArchived, DateTimeOffset ExpectedUpdatedAt);
public sealed record CategoryCreatedResponse(Guid Id);

public sealed record IconResponse(int Id, string Name, string Value, int SortOrder);

public sealed record CategoryPinRequest(bool IsPinned);

public sealed record CategoryPinOrderRequest(IReadOnlyList<Guid> CategoryIds, IReadOnlyList<Guid> ExpectedCategoryIds);
