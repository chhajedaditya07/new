using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace StoreManager.Services.Contacts;

public static class CsvContactReader
{
    public static IReadOnlyList<RawRecord> Read(Stream stream)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            DetectDelimiter = true,        // Excel in some regions saves ';' separated CSV
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim,
        };
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, config);

        var records = new List<RawRecord>();
        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null) return records;
        var headers = csv.HeaderRecord;

        while (csv.Read())
        {
            var values = new Dictionary<string, string>();
            for (var i = 0; i < headers.Length; i++)
                values[headers[i]] = csv.GetField(i) ?? "";
            records.Add(new RawRecord(csv.Parser.Row, values));
        }
        return records;
    }
}
