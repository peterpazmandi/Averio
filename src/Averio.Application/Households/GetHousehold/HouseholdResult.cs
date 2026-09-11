using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Averio.Application.Households.GetHousehold
{
    public sealed record HouseholdResult(
        Guid Id,
        string? Name,
        string? CountryCode,
        string? PreferredCurrency,
        bool OnboardingCompleted,
        string Role);
}