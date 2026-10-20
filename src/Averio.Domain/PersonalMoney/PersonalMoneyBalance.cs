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
                case PersonalMoneyEntryKind.PrivateRefund:
                    // Private outgoings/refunds affect entitlement and attributed private funding.
                    entitlement += entry.Amount;
                    funding += entry.Amount;
                    break;
                case PersonalMoneyEntryKind.SharedCharge:
                case PersonalMoneyEntryKind.SharedRefund:
                case PersonalMoneyEntryKind.Surrender:
                    // Spending from shared funds or giving up rights does not move private cash.
                    entitlement += entry.Amount;
                    break;
            }
        }
        return new PersonalMoneyBalance(entitlement, funding);
    }
}
