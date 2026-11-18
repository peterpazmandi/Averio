namespace Averio.Domain.Recurring;

public enum ReimbursementCalculation { Fixed, Percentage }

/// <summary>Estimates only. External receipt submission is always required.</summary>
public sealed class RecurringReimbursementRule
{
    private RecurringReimbursementRule() { }
    public string Provider { get; private set; } = null!;
    public ReimbursementCalculation Calculation { get; private set; }
    public decimal Value { get; private set; }
    public decimal? MaximumAmount { get; private set; }
    public Guid DestinationAccountId { get; private set; }
    public int PaymentDelayDays { get; private set; }
    public int? SubmissionReminderDays { get; private set; }
    public static RecurringReimbursementRule Create(string provider, ReimbursementCalculation calculation,
        decimal value, decimal? maximumAmount, Guid destinationAccountId, int paymentDelayDays, int? submissionReminderDays)
    {
        if (string.IsNullOrWhiteSpace(provider) || provider.Trim().Length > 160 || !Enum.IsDefined(calculation)
            || !Money(value) || (calculation == ReimbursementCalculation.Percentage && value > 100)
            || (maximumAmount is decimal cap && !Money(cap)) || destinationAccountId == Guid.Empty
            || paymentDelayDays is < 0 or > 365 || submissionReminderDays is < 0 or > 365)
            throw new ArgumentException("Invalid reimbursement configuration.");
        return new() { Provider = provider.Trim(), Calculation = calculation, Value = value, MaximumAmount = maximumAmount,
            DestinationAccountId = destinationAccountId, PaymentDelayDays = paymentDelayDays, SubmissionReminderDays = submissionReminderDays };
    }
    public decimal Estimate(decimal payment)
    {
        if (!Money(payment)) throw new ArgumentException("Invalid payment.");
        var estimate = Calculation == ReimbursementCalculation.Fixed ? Value : decimal.Round(payment * Value / 100m, 4, MidpointRounding.AwayFromZero);
        return Math.Min(payment, Math.Min(estimate, MaximumAmount ?? decimal.MaxValue));
    }
    private static bool Money(decimal value) => value > 0 && value <= 999999999999999.9999m && decimal.Round(value, 4) == value;
}
