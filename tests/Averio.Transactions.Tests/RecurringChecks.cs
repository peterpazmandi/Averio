using Averio.Domain.Recurring;
using Averio.Domain.Transactions;
internal static class RecurringChecks
{
    public static void Calendar()
    {
        Check(RecurringRule.DueDate(new(2028, 1, 31), RecurringFrequency.Monthly, 1) == new DateOnly(2028, 2, 29));
        Check(RecurringRule.DueDate(new(2028, 1, 31), RecurringFrequency.Monthly, 2) == new DateOnly(2028, 3, 31));
        Check(RecurringRule.DueDate(new(2028, 2, 29), RecurringFrequency.Yearly, 1) == new DateOnly(2029, 2, 28));
        Check(RecurringRule.DueDate(new(2028, 2, 29), RecurringFrequency.Yearly, 4) == new DateOnly(2032, 2, 29));
        Check(RecurringRule.DueDate(new(2026, 12, 28), RecurringFrequency.Weekly, 1) == new DateOnly(2027, 1, 4));
        Check(RecurringRule.DueDate(DateOnly.MaxValue, RecurringFrequency.Weekly, 1) is null);
    }
    public static void Lifecycle()
    {
        var now = DateTimeOffset.UtcNow; var day = new DateOnly(2028, 1, 31); var category = Guid.NewGuid();
        var r = RecurringRule.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), category, "Rent", 12.3456m,
            TransactionKind.Expense, TransactionVisibility.Household, RecurringFrequency.Monthly, day, day.AddMonths(1), false, now);
        Check(r.NextDueOn == day && !r.Automatic); var version = r.Version;
        r.Advance(now); Check(r.NextDueOn == new DateOnly(2028, 2, 29) && r.Version != version);
        r.Advance(now); Check(r.NextDueOn is null);
        r.Update(category, "Rent", 12.3456m, r.Visibility, null, false, true, false, now);
        Check(r.IsPaused && r.NextDueOn == new DateOnly(2028, 3, 31));
        r.Block(now); Check(r.IsBlocked);
        r.Update(category, r.Name, r.Amount, r.Visibility, null, true, false, true, now);
        Check(r.IsStopped && r.NextDueOn is null && !r.IsBlocked);
        try { r.Update(category, r.Name, r.Amount, r.Visibility, null, false, false, false, now); }
        catch (ArgumentException) { return; }
        throw new Exception("Stopped schedule reopened");
    }
    private static void Check(bool condition) { if (!condition) throw new Exception("Recurring invariant failed"); }
}
