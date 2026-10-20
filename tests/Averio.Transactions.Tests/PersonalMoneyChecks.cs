using Averio.Domain.Common;
using Averio.Domain.PersonalMoney;

internal static class PersonalMoneyChecks
{
    public static void Ledger()
    {
        var household = Guid.NewGuid();
        var member = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var date = new DateOnly(2026, 10, 17);
        var wallet = PersonalMoneyWallet.Create(household, member, CurrencyCode.Eur, now);
        if (wallet.HouseholdMemberId != member || wallet.Currency != CurrencyCode.Eur)
            throw new Exception("Wallet must be scoped to membership and currency.");

        var transfer = Guid.NewGuid();
        var purchase = Guid.NewGuid();
        var entries = new List<PersonalMoneyEntry>();
        void Add(PersonalMoneyEntryKind kind, decimal value,
            Guid? transferId = null, Guid? transactionId = null)
            => entries.Add(PersonalMoneyEntry.Create(household, wallet.Id, kind,
                value, date, now, transferId, transactionId));

        Add(PersonalMoneyEntryKind.Grant, 300m, transferId: transfer);
        Add(PersonalMoneyEntryKind.Funding, 300m, transferId: transfer);
        Add(PersonalMoneyEntryKind.PrivateCharge, -50m, transactionId: purchase);
        AssertBalance(entries, 250m, 250m);

        // A later funding transfer must not become a second grant.
        Add(PersonalMoneyEntryKind.Funding, 100m, transferId: Guid.NewGuid());
        AssertBalance(entries, 250m, 350m);

        // Date-boundary rollover needs no synthetic entry; negative balances are legal.
        Add(PersonalMoneyEntryKind.PrivateCharge, -270m, transactionId: Guid.NewGuid());
        AssertBalance(entries, -20m, 80m);
        Add(PersonalMoneyEntryKind.Grant, 300m);
        AssertBalance(entries, 280m, 80m);

        // Reversing the original transfer reverses both its grant and funding effects.
        Add(PersonalMoneyEntryKind.Grant, -300m, transferId: transfer);
        Add(PersonalMoneyEntryKind.Funding, -300m, transferId: transfer);
        AssertBalance(entries, -20m, -220m);

        Reject(() => PersonalMoneyEntry.Create(household, wallet.Id,
            PersonalMoneyEntryKind.Funding, 10m, date, now));
        Reject(() => PersonalMoneyEntry.Create(household, wallet.Id,
            PersonalMoneyEntryKind.PrivateCharge, -1.00001m, date, now, transactionId: purchase));
    }

    public static void SharedAndReconciliation()
    {
        var household = Guid.NewGuid();
        var member = Guid.NewGuid();
        var wallet = PersonalMoneyWallet.Create(household, member, CurrencyCode.Huf, DateTimeOffset.UtcNow);
        var date = new DateOnly(2026, 10, 19);
        var now = DateTimeOffset.UtcNow;
        var transfer = Guid.NewGuid();
        var expense = Guid.NewGuid();
        var refund = Guid.NewGuid();
        var events = new List<PersonalMoneyEntry>
        {
            PersonalMoneyEntry.Create(household, wallet.Id, PersonalMoneyEntryKind.Grant, 300m, date, now),
            PersonalMoneyEntry.Create(household, wallet.Id, PersonalMoneyEntryKind.Funding, 300m, date, now, transferId: transfer),
            PersonalMoneyEntry.Create(household, wallet.Id, PersonalMoneyEntryKind.SharedCharge, -80m, date, now, transactionId: expense),
        };
        AssertBalance(events, 220m, 300m); // Shared purchase is one bank expense, not another payout.
        events.Add(PersonalMoneyEntry.Create(household, wallet.Id,
            PersonalMoneyEntryKind.SharedRefund, 20m, date, now, transactionId: refund,
            relatedTransactionId: expense));
        AssertBalance(events, 240m, 300m);
        events.Add(PersonalMoneyEntry.Create(household, wallet.Id,
            PersonalMoneyEntryKind.Funding, -80m, date, now, transferId: Guid.NewGuid()));
        AssertBalance(events, 240m, 220m);
        events.Add(PersonalMoneyEntry.Create(household, wallet.Id,
            PersonalMoneyEntryKind.Surrender, -20m, date, now));
        AssertBalance(events, 220m, 220m);
        events.Add(PersonalMoneyEntry.Create(household, wallet.Id,
            PersonalMoneyEntryKind.PrivateCharge, -50m, date, now, transactionId: Guid.NewGuid()));
        AssertBalance(events, 170m, 170m);
        events.Add(PersonalMoneyEntry.Create(household, wallet.Id,
            PersonalMoneyEntryKind.PrivateRefund, 5m, date, now, transactionId: Guid.NewGuid(),
            relatedTransactionId: expense));
        AssertBalance(events, 175m, 175m);

        Reject(() => PersonalMoneyEntry.Create(household, wallet.Id,
            PersonalMoneyEntryKind.SharedRefund, 5m, date, now, transactionId: refund));
        Reject(() => PersonalMoneyEntry.Create(household, wallet.Id,
            PersonalMoneyEntryKind.SharedCharge, -5m, date, now));
    }

    private static void AssertBalance(IEnumerable<PersonalMoneyEntry> entries,
        decimal entitlement, decimal funding)
    {
        var balance = PersonalMoneyBalance.FromEntries(entries);
        if (balance.Entitlement != entitlement || balance.AttributedPrivateFunding != funding)
            throw new Exception($"Wrong wallet balance: {balance.Entitlement}/{balance.AttributedPrivateFunding}");
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new Exception("Expected validation to reject an invalid ledger entry.");
    }
}
