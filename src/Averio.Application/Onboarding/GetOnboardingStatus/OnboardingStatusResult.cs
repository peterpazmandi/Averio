using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Averio.Application.Onboarding.GetOnboardingStatus
{
    public sealed record OnboardingStatusResult(
        bool Completed,
        string? DisplayName,
        Guid HouseholdId,
        string? HouseholdName,
        string? CountryCode,
        string? PreferredCurrency);
}