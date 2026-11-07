using System.IO.Compression;
using System.Text;
using Averio.Application.Common.Exceptions;
using Averio.Infrastructure.Imports;

internal static class YnabImportChecks
{
    private const string Header =
        "Account,Flag,Date,Payee,Category Group/Category,Category Group,Category,Memo,Outflow,Inflow,Cleared";
    private static MemoryStream Csv(string body)
        => new(Encoding.UTF8.GetBytes("\uFEFF" + Header + "\r\n" + body));
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
    }
    private static void Reject(string csv, string currency = "EUR",
        string date = "MDY", string numbers = "DotDecimal")
    {
        using var file = Csv(csv);
        try { new YnabRegisterReader().Read(file, "register.csv", currency, date, numbers); }
        catch (BusinessRuleException) { return; }
        throw new Exception("YNAB import must reject unsafe input.");
    }
    public static void Run()
    {
        var data = string.Join("\r\n", new[] {
            "Checking,,09/01/2026,Starting Balance,Inflow: Ready to Assign,Inflow,Ready to Assign,,1000.00,0,Cleared",
            "Checking,,09/14/2026,Tesco,Food/Groceries,Food,Groceries,weekly shop,84.20,0,Cleared",
            "Checking,,09/15/2026,Salary,Income/Salary,Income,Salary,,0,1500.00,Reconciled",
            "Checking,,09/14/2026,Tesco,Food/Groceries,Food,Groceries,weekly shop,84.20,0,Cleared",
            "Checking,,09/16/2026,Transfer : Savings,,,,moving funds,200.00,0,Cleared",
            "Savings,,09/16/2026,Transfer : Checking,,,,moving funds,0,200.00,Cleared"
        }) + "\r\n";
        using (var file = Csv(data))
        {
            var result = new YnabRegisterReader().Read(file, "YNAB Register.csv", "EUR", "MDY", "DotDecimal");
            Check(result.Count == 4, "Transfers must collapse to one operation.");
            Check(result.Count(r => r.Kind == "Expense") == 2, "Repeated expenses must remain separate.");
            Check(result.Count(r => r.Kind == "Income") == 1, "Income count changed.");
            Check(result[0].Category == "Food / Groceries" && result[0].Tags == "Tesco"
                && result[0].Note.Contains("weekly shop") && result[0].Amount == 84.20m, "Payee, category, amount or memo lost.");
            var transfer = result.Single(r => r.Kind == "Transfer");
            Check(transfer.Account == "Checking" && transfer.Destination == "Savings"
                && transfer.Amount == 200 && transfer.ReceivedAmount == 200, "Transfer legs not reconciled.");
        }

        var germanHeader = Header.Replace(',', '\t');
        var germanBody = "Giro\t\t14.09.2026\tBäckerei\tEssen/Brot\tEssen\tBrot\tFrühstück\t12,50€\t0\tCleared\r\n";
        using (var file = new MemoryStream(Encoding.UTF8.GetBytes(germanHeader + "\r\n" + germanBody)))
        {
            var result = new YnabRegisterReader().Read(file, "Register.tsv", "EUR", "DMY", "CommaDecimal");
            Check(result.Single().Amount == 12.50m && result.Single().Date == new DateOnly(2026, 9, 14),
                "TSV or day-first format parsed incorrectly.");
        }

        using (var zip = new MemoryStream())
        {
            using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, true))
            {
                void Write(string name, string text)
                {
                    using var writer = new StreamWriter(archive.CreateEntry(name).Open(), Encoding.UTF8);
                    writer.Write(text);
                }
                Write("YNAB Export - Plan Budget.csv", "Month,Category\n09/01/2026,Food\n");
                Write("YNAB Export - Plan Register.csv", Header + "\nChecking,,09/14/2026,Tesco,Food/Groceries,Food,Groceries,,10.00,0,Cleared\n");
            }
            zip.Position = 0;
            var result = new YnabRegisterReader().Read(zip, "YNAB Export.zip", "EUR", "MDY", "DotDecimal");
            Check(result.Count == 1 && result.Single().Amount == 10m, "ZIP register selection failed.");
        }

        Reject("Checking,,09/16/2026,Transfer : Savings,,,,moving,200.00,0,Cleared\n");
        Reject("Checking,,09/16/2026,Starting Balance,Inflow: Ready to Assign,Inflow,Ready to Assign,,1000.00,0,Cleared\n");
        Reject("Checking,,09/16/2026,Tesco,Food/Split,Food,Split,,100.00,0,Cleared\n");
        Reject("Checking,,09/16/2026,Tesco,Split (Multiple Categories),,,,100.00,0,Cleared\\n");
        Reject("Checking,,09/16/2026,Tesco,Food/Groceries,Food,Groceries,,10.00,10.00,Cleared\n");
        Reject("Checking,,09/16/2026,Tesco,Food/Groceries,Food,Groceries,,0,0,Cleared\n");
        Reject("Checking,,14/09/2026,Tesco,Food/Groceries,Food,Groceries,,10.00,0,Cleared\n");
        Reject("Checking,,09/14/2026,Tesco,Food/Groceries,Food,Groceries,,1.12345,0,Cleared\n");
        Reject("Checking,,09/14/2026,Tesco,Food/Groceries,Food,Groceries,,10,00,0,Cleared\n");
        Reject("Checking,,09/14/2026,\"Broken,Food/Groceries,Food,Groceries,,10.00,0,Cleared\n");
        Reject("Checking,,09/14/2026,Tesco,Food/Groceries,Food,Groceries,,10.00,0,Cleared\n", "GBP");
        Console.WriteLine("PASS YNAB CSV/TSV/ZIP, exact amounts, duplicate preservation, paired transfers and unsafe-input guards");
    }
}
