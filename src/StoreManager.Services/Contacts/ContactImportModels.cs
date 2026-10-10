using StoreManager.Core.Entities;

namespace StoreManager.Services.Contacts;

/// <summary>One spreadsheet/vCard record, keyed by normalised header (lower-case alphanumerics only).</summary>
public sealed record RawRecord(int RowNumber, IReadOnlyDictionary<string, string> Values);

public sealed record ContactImportRow(int RowNumber, Contact Contact, IReadOnlyList<string> Tags);

public sealed record ImportIssue(int RowNumber, string Message);

public sealed record ImportPreview(IReadOnlyList<ContactImportRow> ValidRows, IReadOnlyList<ImportIssue> Issues);

public sealed record ImportOutcome(int Created, IReadOnlyList<ImportIssue> Issues);
