namespace Averio.Domain.Transactions;

public enum RefundCreditOrigin { Unknown, Created, ExistingIncome }

// Versioned, private persistence payload. Never expose original income details through refund DTOs.
public sealed record RefundIncomeSnapshot(int SchemaVersion, Guid TransactionId, TransactionItemDraft[] Items)
{
    public static RefundIncomeSnapshot Capture(Transaction credit)
    {
        if (credit.Kind != TransactionKind.Income || credit.Status != TransactionStatus.Posted || credit.HasRefundHistory)
            throw new ArgumentException("An independent posted income is required.");
        return new(1, credit.Id, credit.Items.OrderBy(x => x.Position)
            .Select(x => new TransactionItemDraft(x.Description, x.CategoryId, x.CategoryName, x.CategoryIcon, x.Amount, x.Id)).ToArray());
    }
}
