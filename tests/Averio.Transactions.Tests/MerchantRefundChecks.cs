using Averio.Domain.Transactions;
using Averio.Domain.Common;
internal static class MerchantRefundChecks
{
    public static void Run()
    {
        var now = new DateTimeOffset(2026,11,18,12,0,0,TimeSpan.Zero);
        var h = Guid.NewGuid(); var a = Guid.NewGuid(); var m = Guid.NewGuid(); var c = Guid.NewGuid();
        var purchase = Transaction.Create(h,a,m,TransactionKind.Expense,TransactionVisibility.Household,CurrencyCode.Eur,"Shop",null,new(2026,11,1),
            [new("Shoes",c,"Clothes","shoe",100),new("Food",Guid.NewGuid(),"Groceries","basket",50)],[],now);
        var shoes = purchase.Items.First(); var food = purchase.Items.Last();
        Transaction Credit(decimal amount) => Transaction.Create(h,a,m,TransactionKind.Refund,TransactionVisibility.Household,CurrencyCode.Eur,"Shop",null,new(2026,11,18),
            [new("Return",c,"Clothes","shoe",amount)],[],now);
        var r = ItemRefund.Create(purchase,shoes.Id,Credit(30),Guid.NewGuid(),m,now,new string('A',64));
        purchase.ApplyItemRefund(shoes.Id,r.Amount,now);
        Check(purchase.Amount == 120 && shoes.Amount == 70 && food.Amount == 50 && purchase.OriginalAmount == 150,"Wrong net/category item amount");
        Throws(() => ItemRefund.Create(purchase,shoes.Id,Credit(70.0001m),Guid.NewGuid(),m,now,new string('B',64)));
        purchase.ApplyItemRefund(food.Id,50,now);
        purchase.ApplyItemRefund(shoes.Id,70,now);
        Check(purchase.Amount == 0 && purchase.OriginalAmount == 150 && purchase.Items.Count == 2,"Full multi-item refund lost gross history");
        Throws(() => purchase.Update(a,TransactionKind.Income,purchase.Visibility,purchase.Currency,"Shop",null,purchase.OccurredOn,
            [new("Shoes",c,"Clothes","shoe",100,shoes.Id)],[],now));
        purchase.ApplyItemRefund(shoes.Id,-30,now);
        Check(purchase.Amount == 30 && shoes.RefundedAmount == 70 && shoes.HasRefundHistory,"Reversal did not restore item net cost");
        purchase.Update(a,purchase.Kind,purchase.Visibility,purchase.Currency,"Shop edit",null,purchase.OccurredOn,
            [new("Shoes",c,"Clothes","shoe",100,shoes.Id),new("Food",food.CategoryId,"Groceries","basket",50,food.Id)],[],now);
        Check(purchase.Amount == 30,"Metadata edit applied refunds twice");
        var income = Transaction.Create(h,a,m,TransactionKind.Income,purchase.Visibility,purchase.Currency,"Bank credit",null,new(2026,11,18),
            [new("Incoming",Guid.NewGuid(),"Other income","money",10)],[],now);
        var original = RefundIncomeSnapshot.Capture(income);
        var importId = Guid.NewGuid();
        income.SetImportOrigin(importId);
        income.ClassifyAsRefund(shoes,now);
        Check(income.Kind == TransactionKind.Refund && income.Amount == 10,"Existing credit was still income");
        Throws(() => income.RestoreRefundIncome(original with { SchemaVersion = 2 }, now));
        Throws(() => income.RestoreRefundIncome(original with { TransactionId = Guid.NewGuid() }, now));
        income.RestoreRefundIncome(original, now);
        Check(income.Kind == TransactionKind.Income && income.Status == TransactionStatus.Posted
            && income.Items.Single().Id == original.Items.Single().ExistingItemId
            && income.Items.Single().CategoryId == original.Items.Single().CategoryId
            && income.ImportBatchId == importId && income.Amount == 10, "Restored income lost identity/classification");
        Throws(() => income.RestoreRefundIncome(original, now));
    }
    private static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    private static void Throws(Action action) { try{action();}catch(ArgumentException){return;}throw new Exception("Expected invalid item refund"); }
}
