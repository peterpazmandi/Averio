namespace Averio.Application.Households.UpdateHousehold;

public sealed record UpdateHouseholdCommand(
    Guid HouseholdId, string? Name, string? CountryCode,
    string? PreferredCurrency, Guid ExpectedVersion);
