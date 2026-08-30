namespace Averio.Contracts.Diagnostics;

public sealed record SystemHealthResponse(
    string Status,
    bool DatabaseConnected,
    DateTimeOffset CheckedAtUtc);