using Averio.Contracts.Recurring;
using Averio.Domain.Recurring;
namespace Averio.Api.Mapping;
internal static class ReimbursementConfigurationMapper
{
    public static RecurringReimbursementRule? Parse(RecurringReimbursementConfiguration? r)
    {
        if (r is null) return null;
        try { return RecurringReimbursementRule.Create(r.Provider,
            EnumParser.Parse<ReimbursementCalculation>(r.Calculation, nameof(r.Calculation), "Validation_Reimbursement"),
            DecimalAmountMapper.ParseTransactionAmount(r.Value), r.MaximumAmount is null ? null : DecimalAmountMapper.ParseTransactionAmount(r.MaximumAmount),
            r.DestinationAccountId, r.PaymentDelayDays, r.SubmissionReminderDays); }
        catch (ArgumentException) { throw new Averio.Application.Common.Exceptions.BusinessRuleException("reimbursement_invalid", "Validation_Reimbursement"); }
    }
}
