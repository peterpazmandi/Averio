using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.PersonalMoney;
using Averio.Application.Recurring;
using Averio.Application.Transactions.Common;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Application.Transactions.UpdateTransaction;
using Averio.Application.Transactions.VoidTransaction;
using Averio.Application.Transactions.Refunds;
using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Domain.Categories;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class MerchantItemRefundVerification
{
    public static async Task RunAsync(IServiceProvider services, MutableCurrentUser user, Fixture f, MutableTimeProvider clock)
    {
        var ct = CancellationToken.None;
        user.UserId = f.OwnerUserId;
        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        Guid accountId, categoryB;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var account = Account.Create(f.HouseholdId, [], "Item refund test", AccountType.Checking, AccountScope.Household,
                AccountVisibility.Household, true, CurrencyCode.Eur, 0, day.AddDays(-10), clock.GetUtcNow());
            db.Accounts.Add(account); await db.SaveChangesAsync(ct); accountId = account.Id;
            categoryB = await db.Categories.Where(x => x.IsBase && x.Kind == CategoryKind.Expense && x.Id != TestIds.Groceries).Select(x => x.Id).FirstAsync(ct);
        }
        var purchaseId = await Post(TransactionKind.Expense, [new("Item A",TestIds.Groceries,100),new("Item B",categoryB,200)], day.AddDays(-3));
        var purchase = await Read(purchaseId); var itemA = purchase.Items.Single(x=>x.Description=="Item A").Id; var itemB = purchase.Items.Single(x=>x.Description=="Item B").Id;
        var request = new RecordItemRefund(itemA,25.0001m,day.AddDays(-2),Guid.NewGuid(),purchase.UpdatedAt,true);
        var retries = await Task.WhenAll(Enumerable.Range(0,3).Select(_ => Record(purchaseId,request)));
        Check(retries.Distinct().Count()==1,"Concurrent retry duplicated a refund");
        Check(await Balance(day.AddDays(-3))==-300 && await Balance(day.AddDays(-2))==-274.9999m,"Net expense changed historic bank cash");
        var first = (await Refunds(purchaseId)).Single();
        purchase = await Read(purchaseId);
        Check(purchase.Amount==274.9999m && purchase.Items.Single(x=>x.Id==itemA).Amount==74.9999m && purchase.Items.Single(x=>x.Id==itemB).Amount==200,"Wrong item net amounts");
        Check(purchase.OriginalAmount==300 && purchase.Items.Single(x=>x.Id==itemA).HasRefundHistory,"Gross receipt lost");
        await Throws<BusinessRuleException>(()=>Record(purchaseId,request with { OperationId=Guid.NewGuid(),ExpectedUpdatedAt=purchase.UpdatedAt,Amount=75 }));
        await Throws<BusinessRuleException>(()=>Record(purchaseId,request with { OperationId=Guid.NewGuid(),ExpectedUpdatedAt=purchase.UpdatedAt,TransactionItemId=Guid.NewGuid() }));
        await Throws<BusinessRuleException>(()=>Record(purchaseId,request with { OperationId=Guid.NewGuid(),ExpectedUpdatedAt=purchase.UpdatedAt,MoneyReceived=false }));
        await Throws<BusinessRuleException>(()=>Record(purchaseId,request with { OperationId=Guid.NewGuid(),ExpectedUpdatedAt=purchase.UpdatedAt,ReceivedOn=day.AddDays(1) }));
        await Throws<ConflictException>(()=>Record(purchaseId,request with { OperationId=Guid.NewGuid() }));
        await Record(purchaseId,new(itemB,50,day.AddDays(-1),Guid.NewGuid(),purchase.UpdatedAt,true));
        var creditId = await Post(TransactionKind.Income,[new("Imported return",TestIds.Salary,10)],day);
        var balanceBeforeLink = await Balance(day);
        purchase=await Read(purchaseId); var credit=await Read(creditId);
        await Record(purchaseId,new(itemB,10,day,Guid.NewGuid(),purchase.UpdatedAt,true,creditId,credit.UpdatedAt));
        await using (var scope = services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IMoneyBackRepository>();
            var all = await repository.ListAsync(f.HouseholdId, f.OwnerMemberId, 0, 100, null, ct);
            var page1 = await repository.ListAsync(f.HouseholdId, f.OwnerMemberId, 0, 2, null, ct);
            var page2 = await repository.ListAsync(f.HouseholdId, f.OwnerMemberId, 2, 2, null, ct);
            Check(all.Any(x => x.Kind == "Refund" && x.SourceId == purchaseId)
                && all.Any(x => x.Kind == "Reimbursement")
                && page1.Concat(page2).Select(x => (x.Kind, x.Id)).SequenceEqual(all.Take(4).Select(x => (x.Kind, x.Id))),
                "Money-back mixed pagination lost or reordered rows");
            Check((await repository.ListAsync(f.HouseholdId, f.OwnerMemberId, 0, 100, "Submitted", ct)).All(x => x.Kind != "Refund"),
                "Submitted filter included refunds");
        }
        Check(await Balance(day)==balanceBeforeLink,"Linking an existing credit counted cash twice");
        Check((await Read(creditId)).Kind==TransactionKind.Refund,"Linked credit remains income");
        purchase=await Read(purchaseId);
        Check(purchase.Amount==214.9999m && purchase.Items.Single(x=>x.Id==itemB).Amount==140,"Different item refunds changed wrong categories");
        await using (var scope=services.CreateAsyncScope())
        {
            var imports=scope.ServiceProvider.GetRequiredService<Averio.Application.Imports.IImportRepository>();
            var overlap=await imports.FindOverlapAsync(f.HouseholdId,f.OwnerMemberId,[accountId],day.AddDays(-3),day,ct);
            Check(overlap.Single(x=>x.Id==purchaseId).Amount==300 && overlap.Single(x=>x.Id==creditId).Kind=="Refund", "Import overlap lost original cash amounts/refund classification");
            var protectedIds=await imports.LinkedRecordsAsync(f.HouseholdId,[purchaseId,creditId],ct);
            Check(protectedIds.Contains(purchaseId) && protectedIds.Contains(creditId),"Import undo did not protect item refund dependencies");
        }
        await AssertAggregates(214.9999m,74.9999m,140);
        await using (var scope=services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var activity=Averio.Domain.Activities.Activity.Create(f.HouseholdId,f.OwnerMemberId,"Returned purchase",null,null,clock.GetUtcNow());
            db.Add(activity);
            foreach(var id in new[]{purchaseId,creditId})
            {
                var link=Averio.Domain.Activities.ActivityAssignment.Create(f.HouseholdId,f.OwnerMemberId,id);
                link.Assign(activity,clock.GetUtcNow()); db.Add(link);
            }
            await db.SaveChangesAsync(ct);
            var timeline=await scope.ServiceProvider.GetRequiredService<Averio.Application.Activities.IActivityRepository>().TimelineAsync(f.HouseholdId,f.OwnerMemberId,activity.Id,day,ct);
            Check(timeline.Sum(x=>x.Income)==0 && timeline.Sum(x=>x.Expenses)==214.9999m && timeline.Sum(x=>x.TransactionCount)==1,"Activity counted the refund as income or a second expense");
        }
        await Throws<BusinessRuleException>(()=>EditMetadata(purchase,true));
        await EditMetadata(purchase);
        Check((await Read(purchaseId)).Amount==214.9999m,"Editing applied refunds twice");
        purchase=await Read(purchaseId);
        await Throws<BusinessRuleException>(()=>Void(purchaseId,purchase.UpdatedAt));
        credit=await Read(creditId);
        await Throws<BusinessRuleException>(()=>Void(creditId,credit.UpdatedAt));
        await Cancel(purchaseId,first,purchase.UpdatedAt);
        await Cancel(purchaseId,first,purchase.UpdatedAt);
        purchase=await Read(purchaseId);
        Check(purchase.Amount==240 && await Balance(day)==-240,"Reversal did not restore expense/cash once");
        Check((await Read(first.CreditTransactionId)).Status==TransactionStatus.Voided && (await Refunds(purchaseId)).Single(x=>x.Id==first.Id).CancelledAt!=null,"Reversal lost history");
        await Record(purchaseId,new(itemB,140,day,Guid.NewGuid(),purchase.UpdatedAt,true));
        purchase=await Read(purchaseId);
        await Record(purchaseId,new(itemA,100,day,Guid.NewGuid(),purchase.UpdatedAt,true));
        purchase=await Read(purchaseId);
        Check(purchase.Amount==0 && purchase.Items.All(x=>x.Amount==0) && await Balance(day)==0,"Full receipt refund did not reach zero without income");
        await EditMetadata(purchase); await AssertAggregates(0,0,0);
        // Another member cannot discover or refund the owner's private expense.
        var privateId = await Post(TransactionKind.Expense,[new("Private",TestIds.Groceries,20)],day,f.PrivateAccountId,TransactionVisibility.Private);
        var privatePurchase=await Read(privateId);
        user.UserId=f.MemberUserId;
        await Throws<NotFoundException>(()=>Refunds(privateId));
        await Throws<NotFoundException>(()=>Record(privateId,new(privatePurchase.Items.Single().Id,1,day,Guid.NewGuid(),privatePurchase.UpdatedAt,true)));
        user.UserId=f.OwnerUserId;
        var sharedPurchaseId = await Post(TransactionKind.Expense, [new("Shared return", TestIds.Groceries, 20)], day);
        var movableCreditId = await Post(TransactionKind.Income, [new("Returned credit", TestIds.Salary, 10)], day);
        var sharedPurchase = await Read(sharedPurchaseId);
        var movableCredit = await Read(movableCreditId);
        await Record(sharedPurchaseId, new(sharedPurchase.Items.Single().Id, 10, day, Guid.NewGuid(), sharedPurchase.UpdatedAt, true, movableCreditId, movableCredit.UpdatedAt));
        var returned = (await Refunds(sharedPurchaseId)).Single();
        sharedPurchase = await Read(sharedPurchaseId);
        await Cancel(sharedPurchaseId, returned, sharedPurchase.UpdatedAt);
        movableCredit = await Read(movableCreditId);
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<UpdateTransactionHandler>().HandleAsync(new(f.HouseholdId, movableCreditId, f.PrivateAccountId,
                movableCredit.Kind, TransactionVisibility.Private, movableCredit.Merchant, movableCredit.Note, movableCredit.OccurredOn,
                movableCredit.Items.Select(i => new TransactionItemInput(i.Description, i.CategoryId, i.Amount, i.Id)).ToArray(), [], movableCredit.UpdatedAt), ct);
        Check((await Refunds(sharedPurchaseId)).Any(x => x.Id == returned.Id), "Owner lost visible refund history");
        user.UserId=f.MemberUserId;
        Check((await Refunds(sharedPurchaseId)).All(x => x.Id != returned.Id), "Refund history exposed an inaccessible historical credit");
        user.UserId=f.OwnerUserId;
        // A partially attributed item restores only the same share of allowance.
        var available=await Allowance();
        var allocatedId=await Post(TransactionKind.Expense,[new("Allocated",TestIds.Groceries,100,PersonalMoneyAmount:40)],day);
        var allocated=await Read(allocatedId);
        await Record(allocatedId,new(allocated.Items.Single().Id,25,day,Guid.NewGuid(),allocated.UpdatedAt,true));
        Check(await Allowance()==available-30,"Item refund did not restore proportional allowance");
        allocated=await Read(allocatedId);
        await Throws<BusinessRuleException>(()=>EditAllocation(allocated,20));
        await EditAllocation(allocated,40);
        var allocationRefund=(await Refunds(allocatedId)).Single();
        allocated=await Read(allocatedId); await EditMetadata(allocated);
        Check(await Allowance()==available-30,"Metadata edit lost allowance refund");
        allocated=await Read(allocatedId);
        await Cancel(allocatedId,allocationRefund,allocated.UpdatedAt);
        Check(await Allowance()==available-40,"Refund reversal did not reverse allowance credit");
        var beforeUnlink=await Allowance();
        var unlinkId=await Post(TransactionKind.Expense,[new("Unlink after return",TestIds.Groceries,100,PersonalMoneyAmount:40)],day);
        var unlink=await Read(unlinkId);
        await Record(unlinkId,new(unlink.Items.Single().Id,25,day,Guid.NewGuid(),unlink.UpdatedAt,true));
        unlink=await Read(unlinkId);
        await EditAllocation(unlink,0);
        Check(await Allowance()==beforeUnlink,"Unlink did not reverse the item allowance and its refund attribution together");
        unlink=await Read(unlinkId);
        await Throws<BusinessRuleException>(()=>EditAllocation(unlink,10));
        await Cancel(unlinkId,(await Refunds(unlinkId)).Single(),unlink.UpdatedAt);
        Check(await Allowance()==beforeUnlink,"Cancelling a refund after unlink double-reversed private allowance");

        // Another receipt item's allocation must never cover an unlinked item's refund.
        var beforeMultiple = await Allowance();
        var multipleId = await Post(TransactionKind.Expense,
            [new("Allocated A",TestIds.Groceries,100,PersonalMoneyAmount:50),
             new("Allocated B",categoryB,100,PersonalMoneyAmount:50)],day);
        var multiple = await Read(multipleId);
        var multipleA = multiple.Items.Single(x=>x.Description=="Allocated A").Id;
        var multipleB = multiple.Items.Single(x=>x.Description=="Allocated B").Id;
        await Record(multipleId,new(multipleA,100,day,Guid.NewGuid(),multiple.UpdatedAt,true));
        multiple = await Read(multipleId);
        await EditAllocation(multiple,0,multipleA);
        Check(await Allowance()==beforeMultiple-50,"Another item's allocation retained the unlinked item's refund credit");
        multiple = await Read(multipleId);
        await Record(multipleId,new(multipleB,100,day,Guid.NewGuid(),multiple.UpdatedAt,true));
        Check(await Allowance()==beforeMultiple,"The remaining item's refund was blocked by an unlinked item's old credit");
        multiple = await Read(multipleId);
        await Cancel(multipleId,(await Refunds(multipleId)).Single(x=>x.TransactionItemId==multipleA),multiple.UpdatedAt);
        Check(await Allowance()==beforeMultiple,"Reversing an unlinked item's refund reduced another item's restored allowance");
        multiple = await Read(multipleId);
        await Cancel(multipleId,(await Refunds(multipleId)).Single(x=>x.TransactionItemId==multipleB),multiple.UpdatedAt);
        Check(await Allowance()==beforeMultiple-50,"The retained item's refund did not reverse exactly once");

        var beforePartial = await Allowance();
        var partialId = await Post(TransactionKind.Expense,
            [new("Partial A",TestIds.Groceries,100,PersonalMoneyAmount:40),
             new("Partial B",categoryB,100,PersonalMoneyAmount:40)],day);
        var partial = await Read(partialId);
        var partialA = partial.Items.Single(x=>x.Description=="Partial A").Id;
        var partialB = partial.Items.Single(x=>x.Description=="Partial B").Id;
        await Record(partialId,new(partialA,25,day,Guid.NewGuid(),partial.UpdatedAt,true));
        partial = await Read(partialId);
        await Record(partialId,new(partialB,50,day,Guid.NewGuid(),partial.UpdatedAt,true));
        partial = await Read(partialId);
        await EditAllocation(partial,0,partialA);
        Check(await Allowance()==beforePartial-20,"Unlinking one partially refunded item changed another item's refund attribution");
        partial = await Read(partialId);
        await Cancel(partialId,(await Refunds(partialId)).Single(x=>x.TransactionItemId==partialA),partial.UpdatedAt);
        Check(await Allowance()==beforePartial-20,"An unlinked partial refund was reversed twice");
        partial = await Read(partialId);
        await Cancel(partialId,(await Refunds(partialId)).Single(x=>x.TransactionItemId==partialB),partial.UpdatedAt);
        Check(await Allowance()==beforePartial-40,"A different item's partial refund was not retained for reversal");
        var beforeTiny = await Allowance();
        var tinyId = await Post(TransactionKind.Expense,[new("Precise share",TestIds.Groceries,0.001m,PersonalMoneyAmount:0.0004m)],day);
        for (var i=0;i<10;i++)
        {
            var tiny=await Read(tinyId);
            await Record(tinyId,new(tiny.Items.Single().Id,0.0001m,day,Guid.NewGuid(),tiny.UpdatedAt,true));
        }
        Check((await Read(tinyId)).Amount==0 && await Allowance()==beforeTiny,"Repeated fractional returns accumulated allowance rounding drift");
        Guid largeAccountId;
        await using (var scope=services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var large=Account.Create(f.HouseholdId,[],"Large refund precision",AccountType.Cash,AccountScope.Household,AccountVisibility.Household,true,CurrencyCode.Eur,0,day,clock.GetUtcNow());
            db.Accounts.Add(large); await db.SaveChangesAsync(ct); largeAccountId=large.Id;
        }
        var beforeLarge=await Allowance();
        var maximum=999999999999999.9999m;
        var largeId=await Post(TransactionKind.Expense,[new("Maximum cost",TestIds.Groceries,maximum,PersonalMoneyAmount:maximum)],day,largeAccountId);
        var largePurchase=await Read(largeId);
        await Record(largeId,new(largePurchase.Items.Single().Id,maximum,day,Guid.NewGuid(),largePurchase.UpdatedAt,true));
        Check((await Read(largeId)).Amount==0 && await Allowance()==beforeLarge,"Maximum-precision refund overflowed or lost allowance value");
        Console.WriteLine("PASS merchant item refunds: partial/multiple/full returns, exact per-item/category net costs, no income, unchanged linked cash, dated balances, retries, privacy, protected edits, reversal/history and proportional allowance");

        async Task<Guid> Post(TransactionKind kind,IReadOnlyList<TransactionItemInput> items,DateOnly date,Guid? account=null,TransactionVisibility visibility=TransactionVisibility.Household) {
            await using var s=services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<CreateTransactionHandler>().HandleAsync(new(f.HouseholdId,account??accountId,kind,visibility,"Merchant refund test",null,date,items,[]),ct);
        }
        async Task<Transaction> Read(Guid id) { await using var s=services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<AverioDbContext>().Transactions.AsNoTracking().Include(x=>x.Items).SingleAsync(x=>x.Id==id,ct); }
        async Task<Guid> Record(Guid id,RecordItemRefund input) { await using var s=services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<ItemRefundHandler>().RecordAsync(f.HouseholdId,id,input,ct); }
        async Task<IReadOnlyList<ItemRefund>> Refunds(Guid id) { await using var s=services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<ItemRefundHandler>().ListAsync(f.HouseholdId,id,ct); }
        async Task Cancel(Guid id,ItemRefund refund,DateTimeOffset version) { await using var s=services.CreateAsyncScope(); await s.ServiceProvider.GetRequiredService<ItemRefundHandler>().CancelAsync(f.HouseholdId,id,refund.Id,refund.Version,version,ct); }
        async Task Void(Guid id,DateTimeOffset version) { await using var s=services.CreateAsyncScope(); await s.ServiceProvider.GetRequiredService<VoidTransactionHandler>().HandleAsync(f.HouseholdId,id,version,ct); }
        async Task<decimal> Balance(DateOnly date) { await using var s=services.CreateAsyncScope(); return (await s.ServiceProvider.GetRequiredService<IAccountBalanceReader>().GetCurrentBalancesAsync(f.HouseholdId,f.OwnerMemberId,[accountId],date,ct))[accountId]; }
        async Task<decimal> Allowance() { await using var s=services.CreateAsyncScope(); return (await s.ServiceProvider.GetRequiredService<PersonalMoneyHandler>().GetMineAsync(f.HouseholdId,ct))!.Available; }
        async Task EditMetadata(Transaction p,bool sendNet=false) {
            await using var s=services.CreateAsyncScope(); await s.ServiceProvider.GetRequiredService<UpdateTransactionHandler>().HandleAsync(new(f.HouseholdId,p.Id,p.AccountId,p.Kind,p.Visibility,"Edited merchant",null,p.OccurredOn,
                p.Items.OrderBy(x=>x.Position).Select(x=>new TransactionItemInput(x.Description,x.CategoryId,sendNet?x.Amount:x.OriginalAmount,x.Id)).ToArray(),[],p.UpdatedAt),ct);
        }
        async Task EditAllocation(Transaction p,decimal amount,Guid? itemId=null) {
            await using var s=services.CreateAsyncScope(); await s.ServiceProvider.GetRequiredService<UpdateTransactionHandler>().HandleAsync(new(f.HouseholdId,p.Id,p.AccountId,p.Kind,p.Visibility,p.Merchant,p.Note,p.OccurredOn,
                p.Items.OrderBy(x=>x.Position).Select(x=>new TransactionItemInput(x.Description,x.CategoryId,x.OriginalAmount,x.Id,itemId is null || x.Id==itemId ? amount : null)).ToArray(),[],p.UpdatedAt),ct);
        }
        async Task AssertAggregates(decimal expense,decimal a,decimal b) {
            await using var s=services.CreateAsyncScope(); var db=s.ServiceProvider.GetRequiredService<AverioDbContext>();
            var rows=await db.TransactionAggregates.Where(x=>x.AccountId==accountId && x.Period==TransactionAggregatePeriod.Month).ToArrayAsync(ct);
            Check(rows.Where(x=>x.DimensionType==TransactionAggregateDimension.Account).Sum(x=>x.IncomeAmount)==0,"Refund inflated income");
            Check(rows.Where(x=>x.DimensionType==TransactionAggregateDimension.Account).Sum(x=>x.ExpenseAmount)==expense,"Wrong net expense total");
            Check(rows.Where(x=>x.DimensionType==TransactionAggregateDimension.Category && x.DimensionId==TestIds.Groceries).Sum(x=>x.ExpenseAmount)==a,"Wrong first category total");
            Check(rows.Where(x=>x.DimensionType==TransactionAggregateDimension.Category && x.DimensionId==categoryB).Sum(x=>x.ExpenseAmount)==b,"Wrong second category total");
        }
    }
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static async Task Throws<T>(Func<Task> action) where T:Exception {try{await action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
}
