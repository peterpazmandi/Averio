using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Averio.Application.Households.UpdateHousehold
{
    public sealed record UpdateHouseholdCommand(
        Guid HouseholdId,
        string? Name,
        string? CountryCode,
        string? PreferredCurrency);
}