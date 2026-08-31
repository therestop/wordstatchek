using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace WordstatCheck.Core;

public static class ExportService
{
    private static readonly string[] Headers = ["phrase", "total_count", "status", "error", "checked_at"];

    public static void Export(IReadOnlyList<CheckResult> results, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        WriteCsv(System.IO.Path.Combine(outputDirectory, "wordstat_all.csv"), results);
        WriteCsv(System.IO.Path.Combine(outputDirectory, "wordstat_nonzero.csv"), results.Where(IsNonzero));
        WriteLines(System.IO.Path.Combine(outputDirectory, "wordstat_nonzero.txt"), results.Where(IsNonzero).Select(x => x.Phrase));
        WriteLines(System.IO.Path.Combine(outputDirectory, "wordstat_zero.txt"), results.Where(x => x.Status == "zero").Select(x => x.Phrase));
        WriteLines(System.IO.Path.Combine(outputDirectory, "wordstat_errors.txt"), results.Where(x => x.Status == "error").Select(x => $"{x.Phrase}\t{x.Error}"));
        WriteWorkbook(System.IO.Path.Combine(outputDirectory, "wordstat_results.xlsx"), results);
    }

    private static bool IsNonzero(CheckResult value) => value.Status == "nonzero";

    private static void WriteCsv(string path, IEnumerable<CheckResult> results)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine(string.Join(',', Headers));
        foreach (var item in results)
        {
            writer.WriteLine(string.Join(',',
                Csv(item.Phrase),
                item.TotalCount?.ToString(CultureInfo.InvariantCulture) ?? "",
                Csv(item.Status),
                Csv(item.Error),
                Csv(item.CheckedAt.ToString("O"))));
        }
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static void WriteLines(string path, IEnumerable<string> lines) =>
        File.WriteAllLines(path, lines, new UTF8Encoding(false));

    private static void WriteWorkbook(string path, IReadOnlyList<CheckResult> results)
    {
        using var workbook = new XLWorkbook();
        AddSheet(workbook, "Все", results);
        AddSheet(workbook, "Ненулевые", results.Where(IsNonzero));
        AddSheet(workbook, "Нулевые", results.Where(x => x.Status == "zero"));
        AddSheet(workbook, "Ошибки", results.Where(x => x.Status == "error"));
        workbook.SaveAs(path);
    }

    private static void AddSheet(XLWorkbook workbook, string name, IEnumerable<CheckResult> source)
    {
        var sheet = workbook.Worksheets.Add(name);
        for (var column = 0; column < Headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = Headers[column];
            sheet.Cell(1, column + 1).Style.Font.Bold = true;
        }
        var row = 2;
        foreach (var item in source)
        {
            sheet.Cell(row, 1).Value = item.Phrase;
            if (item.TotalCount is not null) sheet.Cell(row, 2).Value = item.TotalCount.Value;
            sheet.Cell(row, 3).Value = item.Status;
            sheet.Cell(row, 4).Value = item.Error;
            sheet.Cell(row, 5).Value = item.CheckedAt.UtcDateTime;
            row++;
        }
        sheet.SheetView.FreezeRows(1);
        sheet.RangeUsed()?.SetAutoFilter();
        sheet.Column(1).Width = 52;
        sheet.Column(2).Width = 16;
        sheet.Column(3).Width = 14;
        sheet.Column(4).Width = 55;
        sheet.Column(5).Width = 25;
    }
}

