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

    public static void MonthlyRecurring()
    {
        var household = Guid.NewGuid();
        var member = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var start = new DateOnly(2026, 1, 31);
        var rule = RecurringAllowanceRule.Create(household, member, CurrencyCode.Eur,
            300m, start, new DateOnly(2026, 4, 30), Guid.NewGuid(), now);
        if (rule.NextDueOn != start || rule.Version == Guid.Empty)
            throw new Exception("Monthly schedule must have a stable first due date and version.");
        rule.Advance(now);
        if (rule.NextDueOn != new DateOnly(2026, 2, 28))
            throw new Exception("January 31 must clamp to February 28.");
        rule.Advance(now);
        if (rule.NextDueOn != new DateOnly(2026, 3, 31))
            throw new Exception("March due day must use the original anchor, not the clamped February date.");
        rule.Update(350m, rule.EndsOn, true, now);
        if (!rule.IsPaused || rule.NextDueOn != new DateOnly(2026, 3, 31))
            throw new Exception("Pausing must preserve unpaid due occurrences.");
        rule.Update(350m, rule.EndsOn, false, now);
        rule.Advance(now);
        rule.Advance(now);
        if (rule.NextDueOn is not null)
            throw new Exception("Completed rules must have no next occurrence.");
        var completed = RecurringAllowanceRule.Create(household, member, CurrencyCode.Eur,
            15m, start, start, Guid.NewGuid(), now);
        completed.Advance(now);
        if (completed.NextDueOn is not null)
            throw new Exception("Single-month schedule must complete after posting.");
        Reject(() => completed.Update(20m, null, false, now));
        rule.Stop(now);
        if (!rule.IsStopped || rule.NextDueOn is not null)
            throw new Exception("Stop must permanently disable a schedule.");
        Reject(() => RecurringAllowanceRule.Create(household, member, CurrencyCode.Eur,
            -1m, start, null, Guid.NewGuid(), now));
        Reject(() => RecurringAllowanceRule.Create(household, member, CurrencyCode.Eur,
            300m, start, start.AddDays(-1), Guid.NewGuid(), now));
        var leapStart = new DateOnly(2028, 1, 31);
        if (RecurringAllowanceRule.MonthlyDueDate(leapStart, 1) != new DateOnly(2028, 2, 29)
            || RecurringAllowanceRule.MonthlyDueDate(leapStart, 2) != new DateOnly(2028, 3, 31))
            throw new Exception("Leap-year monthly anchoring failed.");
        var wallet = PersonalMoneyWallet.Create(household, member, CurrencyCode.Eur, now);
        var event1 = PersonalMoneyEntry.Create(household, wallet.Id, PersonalMoneyEntryKind.Grant,
            300m, start, now, recurringAllowanceRuleId: rule.Id);
        var event2 = PersonalMoneyEntry.Create(household, wallet.Id, PersonalMoneyEntryKind.Grant,
            350m, new DateOnly(2026, 2, 28), now, recurringAllowanceRuleId: rule.Id);
        AssertBalance([event1, event2], 650m, 0m);
        Reject(() => PersonalMoneyEntry.Create(household, wallet.Id,
            PersonalMoneyEntryKind.Funding, 300m, start, now, transferId: Guid.NewGuid(),
            recurringAllowanceRuleId: rule.Id));
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
