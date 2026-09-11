using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Averio.Infrastructure.Onboarding.CompleteOnboarding
{
    public sealed record CompleteOnboardingCommand(
        string DisplayName,
        string HouseholdName,
        string CountryCode,
        string PreferredCurrency);
}