using System.Globalization;
using Averio.Application.Activities;
using Averio.Application.Receipts;
using Averio.Application.Transactions.Common;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Domain.Transactions;

internal static class ReceiptConfirmationChecks
{
    public static void Run()
    {
        var version = Guid.NewGuid();
        var category = Guid.NewGuid();
        var request = new CreateTransactionCommand(Guid.NewGuid(), Guid.NewGuid(), TransactionKind.Expense,
            TransactionVisibility.Household, "Shop", null, new DateOnly(2026, 10, 28),
            [new TransactionItemInput("One", category, 1.2m), new TransactionItemInput("Two", category, 3.4m)],
            [Guid.NewGuid(), Guid.NewGuid()]);
        string Fingerprint(CreateTransactionCommand command, decimal total = 4.6m)
            => ReceiptConfirmationFingerprint.Create(version, "EUR", total, command);
        var fingerprint = Fingerprint(request);
        Check(ReceiptConfirmationFingerprint.Create(version, "EUR", 4.6m, request, []) == fingerprint,
            "empty rules must preserve old confirmation fingerprints");
        var rules = new RememberReceiptCategory[] { new(0, 0), new(2, 1) };
        var withRules = ReceiptConfirmationFingerprint.Create(version, "EUR", 4.6m, request, rules);
        Check(withRules != fingerprint, "sharing consent must be part of confirmation intent");
        Check(withRules == ReceiptConfirmationFingerprint.Create(version, "EUR", 4.6m, request, rules.Reverse().ToArray()),
            "rule selection order has no meaning");
        Check(withRules != ReceiptConfirmationFingerprint.Create(version, "EUR", 4.6m, request, [new(0, 1), new(2, 0)]),
            "changed source mapping must conflict");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("hu-HU");
            var equivalent = request with { Merchant = " Shop ", Note = " ",
                Items = [new TransactionItemInput(" One ", category, 1.2000m), new TransactionItemInput("Two", category, 3.4000m)],
                LabelIds = request.LabelIds.Reverse().ToArray() };
            Check(Fingerprint(equivalent, 4.6000m) == fingerprint, "decimal scale, request culture, trimmed text and label order must not break a legitimate retry");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        Check(Fingerprint(request with {
            Items = [new TransactionItemInput("One", category, 2.2m), new TransactionItemInput("Two", category, 2.4m)] }) != fingerprint,
            "equal totals must not hide changed item allocations");
        Check(Fingerprint(request with {
            Items = [new TransactionItemInput("One", category, 1.2m, PersonalMoneyAmount: 0.5m), request.Items[1]] }) != fingerprint,
            "changed Personal Money intent must conflict");
        Check(Fingerprint(request with { ActivityAssignment = new ActivityAssignmentInput(Guid.NewGuid(), Guid.Empty) }) != fingerprint,
            "changed private activity assignment must conflict");
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}

