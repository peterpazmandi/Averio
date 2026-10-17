namespace Averio.Domain.PersonalMoney;

public sealed record PersonalMoneyBalance(decimal Entitlement, decimal AttributedPrivateFunding)
{
    public decimal FundingDifference => Entitlement - AttributedPrivateFunding;

    public static PersonalMoneyBalance FromEntries(IEnumerable<PersonalMoneyEntry> entries)
    {
        decimal entitlement = 0m, funding = 0m;
        foreach (var entry in entries)
        {
            switch (entry.Kind)
            {
                case PersonalMoneyEntryKind.Grant:
                    entitlement += entry.Amount;
                    break;
                case PersonalMoneyEntryKind.Funding:
                    funding += entry.Amount;
                    break;
                case PersonalMoneyEntryKind.PrivateCharge:
                    // A signed private charge reduces both entitlement and attributed funding.
                    entitlement += entry.Amount;
                    funding += entry.Amount;
                    break;
            }
        }
        return new PersonalMoneyBalance(entitlement, funding);
    }
}
