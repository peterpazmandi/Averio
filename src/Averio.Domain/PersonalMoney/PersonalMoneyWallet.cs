using Averio.Domain.Common;

namespace Averio.Domain.PersonalMoney;

// An entitlement identity, never a financial account or an independently mutable balance.
public sealed class PersonalMoneyWallet
{
    private PersonalMoneyWallet() { }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid HouseholdMemberId { get; private set; }
    public CurrencyCode Currency { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static PersonalMoneyWallet Create(Guid householdId, Guid memberId,
        CurrencyCode currency, DateTimeOffset now)
    {
        if (householdId == Guid.Empty || memberId == Guid.Empty
            || !CurrencyCode.IsSupported(currency.Value))
            throw new ArgumentException("A wallet needs a household, member and supported currency.");
        return new PersonalMoneyWallet
        {
            Id = Guid.NewGuid(), HouseholdId = householdId, HouseholdMemberId = memberId,
            Currency = currency, CreatedAt = now
        };
    }
}
