namespace Averio.Domain.Accounts;

public sealed class AccountOwner
{
    private AccountOwner() { }

    internal AccountOwner(Guid householdId, Guid accountId, Guid householdMemberId)
    {
        if (householdId == Guid.Empty || accountId == Guid.Empty
            || householdMemberId == Guid.Empty)
        {
            throw new ArgumentException("Owner identifiers must not be empty.");
        }

        HouseholdId = householdId;
        AccountId = accountId;
        HouseholdMemberId = householdMemberId;
    }

    public Guid HouseholdId { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid HouseholdMemberId { get; private set; }
}
