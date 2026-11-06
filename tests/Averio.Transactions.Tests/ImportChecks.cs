using System.IO.Compression;
using System.Xml.Linq;
using Averio.Application.Common.Exceptions;
using Averio.Infrastructure.Imports;

internal static class ImportChecks
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly string[] Headers = ["Dátum és idő", "Kategória", "Számla", "Összeg alapértelmezett pénznemben", "Alapértelmezett pénznem", "Összeg a számla pénznemében", "Számla pénzneme", "Tranzakció összege a tranzakció pénznemében", "Tranzakció pénzneme", "Címkék", "Megjegyzés"];
    public static void Run()
    {
        var reader = new MoneyManagerXlsxReader();
        using var valid = Workbook();
        var rows = reader.Read(valid);
        if (rows.Count != 4 || rows[0].Amount != 1234.5678m || rows[0].Account != "Main"
            || rows[0].Tags != "Shop" || rows[0].Date != new DateOnly(2026, 9, 21)
            || rows[2].Kind != "Income" || rows[3].ReceivedAmount is not null
            || rows[0] with { RowNumber = 0 } != rows[1] with { RowNumber = 0 })
            throw new Exception("Import lost precision, repeated rows or source fields.");
        foreach (var mutation in new[] { "negative", "precision", "formula", "headers", "time", "fx", "date1904", "external", "empty", "malformed" })
        {
            using var file = Workbook(mutation);
            try { reader.Read(file); }
            catch (BusinessRuleException) { continue; }
            throw new Exception($"Expected rejection: {mutation}");
        }
        Console.WriteLine("PASS XLSX precision, source mapping, repeated rows and invalid input guards");
    }
    public static MemoryStream Workbook(string mutation = "")
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void Write(string name, string value) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(value); }
            Write("xl/workbook.xml", $"<workbook xmlns='{S}' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><workbookPr date1904='{(mutation == "date1904" ? "1" : "0")}'/><sheets><sheet name='Kiadások' r:id='r1'/><sheet name='Bevétel' r:id='r2'/><sheet name='Átutalás' r:id='r3'/></sheets></workbook>");
            Write("xl/_rels/workbook.xml.rels", "<Relationships>" + string.Concat(Enumerable.Range(1, 3).Select(i => $"<Relationship Id='r{i}' Target='worksheets/sheet{i}.xml' {(mutation == "external" ? "TargetMode='External'" : "")}/>")) + "</Relationships>");
            for (var i = 1; i <= 3; i++)
            {
                var headers = i == 3 ? new[] { "Dátum és idő", "Kimenő", "Beérkező", "Összeg kimenő pénznemben", "Kimenő pénznem", "Összeg bejövő pénznemben", "Bejövő pénznem", "Megjegyzés" } : Headers.ToArray();
                if (mutation == "headers") headers[0] = "Unknown";
                var amount = mutation switch { "negative" => "-1", "precision" => "1.00001", _ => "1234.5678" };
                var values = i == 3 ? new[] { "46286", "Main", "Savings", "10", "EUR", "", "", "transfer" }
                    : new[] { mutation == "time" ? "46286.5" : "46286", "Food", " Main ", amount, "EUR", amount, mutation == "fx" ? "HUF" : "EUR", "", "", "Shop", "note" };
                XElement Row(int number, string[] cells) => new(S + "row", new XAttribute("r", number), cells.Select((v, index) => new XElement(S + "c", new XAttribute("r", $"{(char)('A' + index)}{number}"), new XAttribute("t", "inlineStr"), new XElement(S + "is", new XElement(S + "t", v)))));
                var data = new XElement(S + "sheetData", Row(2, headers));
                if (mutation != "empty") { data.Add(Row(3, values)); if (i == 1) data.Add(Row(4, values)); }
                if (mutation == "formula") data.Descendants(S + "c").First().Add(new XElement(S + "f", "1+1"));
                Write($"xl/worksheets/sheet{i}.xml", mutation == "malformed" ? "bad xml" : new XElement(S + "worksheet", data).ToString());
            }
        }
        stream.Position = 0;
        return stream;
    }
}
