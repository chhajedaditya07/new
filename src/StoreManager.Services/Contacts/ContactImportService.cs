using StoreManager.Core.Abstractions;
using StoreManager.Core.Models;

namespace StoreManager.Services.Contacts;

public enum ImportFormat { Csv, Excel, VCard }

public sealed class ContactImportService
{
    private readonly IContactRepository _contacts;

    public ContactImportService(IContactRepository contacts) => _contacts = contacts;

    public static ImportFormat? DetectFormat(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".csv" or ".txt" => ImportFormat.Csv,
            ".xlsx" or ".xlsm" => ImportFormat.Excel,
            ".vcf" or ".vcard" => ImportFormat.VCard,
            _ => null
        };

    /// <summary>Parses and validates without touching the database, so the user can review problems first.</summary>
    public ImportPreview Preview(Stream stream, ImportFormat format)
    {
        IReadOnlyList<RawRecord> records = format switch
        {
            ImportFormat.Csv => CsvContactReader.Read(stream),
            ImportFormat.Excel => ExcelContactReader.Read(stream),
            ImportFormat.VCard => VCardContactReader.Read(stream),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        return ContactRecordMapper.Map(records);
    }

    /// <summary>Imports the valid rows in a single transaction. Numbers that already exist are skipped and reported.</summary>
    public async Task<ImportOutcome> ImportAsync(ImportPreview preview, CancellationToken ct = default)
    {
        var rows = preview.ValidRows;
        var result = await _contacts.BulkCreateAsync(
            rows.Select(r => new BulkContact(r.Contact, r.Tags)).ToList(), ct);

        var issues = preview.Issues.ToList();
        foreach (var (index, reason) in result.Skipped)
            issues.Add(new ImportIssue(rows[index].RowNumber, reason.Contains("Duplicate") ? "Mobile already exists in the app" : reason));
        return new ImportOutcome(result.Created, issues.OrderBy(i => i.RowNumber).ToList());
    }
}
