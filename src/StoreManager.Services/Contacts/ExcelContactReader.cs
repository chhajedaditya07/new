using ClosedXML.Excel;

namespace StoreManager.Services.Contacts;

public static class ExcelContactReader
{
    /// <summary>Reads the first worksheet; the first non-empty row is the header.</summary>
    public static IReadOnlyList<RawRecord> Read(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.FirstOrDefault();
        var records = new List<RawRecord>();
        if (ws is null) return records;

        var used = ws.RangeUsed();
        if (used is null) return records;

        var firstRow = used.FirstRow().RowNumber();
        var lastRow = used.LastRow().RowNumber();
        var firstCol = used.FirstColumn().ColumnNumber();
        var lastCol = used.LastColumn().ColumnNumber();

        var headers = new Dictionary<int, string>();
        for (var c = firstCol; c <= lastCol; c++)
        {
            var h = ws.Cell(firstRow, c).GetString().Trim();
            if (h.Length > 0) headers[c] = h;
        }

        for (var r = firstRow + 1; r <= lastRow; r++)
        {
            var values = new Dictionary<string, string>();
            foreach (var (col, header) in headers)
                values[header] = CellText(ws.Cell(r, col));
            records.Add(new RawRecord(r, values));
        }
        return records;
    }

    private static string CellText(IXLCell cell)
    {
        if (cell.IsEmpty()) return "";
        if (cell.DataType == XLDataType.DateTime) return cell.GetDateTime().ToString("yyyy-MM-dd");
        if (cell.DataType == XLDataType.Number)
        {
            var d = cell.GetDouble();
            // Phone numbers typed as numbers must not become "9.8765E+09" or "9876543210.0".
            return d == Math.Floor(d) && Math.Abs(d) < 1e15 ? ((long)d).ToString() : d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return cell.GetString().Trim();
    }
}
