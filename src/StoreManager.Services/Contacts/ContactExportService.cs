using ClosedXML.Excel;
using StoreManager.Core.Entities;
using StoreManager.Core.Formatting;

namespace StoreManager.Services.Contacts;

public static class ContactExportService
{
    /// <summary>Columns use the same names the importer understands, so an export can be edited and re-imported.</summary>
    public static readonly string[] Headers =
        { "Name", "Type", "Mobile", "WhatsApp", "Alternate", "City", "Address", "GSTIN", "Birthday", "Anniversary", "Tags", "Notes" };

    public static void WriteExcel(IEnumerable<Contact> contacts, Stream output)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Contacts");
        for (var i = 0; i < Headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = Headers[i];
            cell.Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var c in contacts)
        {
            // Phone numbers and dates are written as text so Excel never reformats them.
            SetText(ws, row, 1, c.Name);
            SetText(ws, row, 2, c.Type.ToString());
            SetText(ws, row, 3, c.Mobile);
            SetText(ws, row, 4, c.WhatsApp);
            SetText(ws, row, 5, c.AltPhone);
            SetText(ws, row, 6, c.City);
            SetText(ws, row, 7, c.Address);
            SetText(ws, row, 8, c.Gstin);
            SetText(ws, row, 9, IstDate.Date(c.Birthday));
            SetText(ws, row, 10, IstDate.Date(c.Anniversary));
            SetText(ws, row, 11, string.Join(", ", c.ContactTags.Select(t => t.Tag.Name).OrderBy(n => n)));
            SetText(ws, row, 12, c.Notes);
            row++;
        }
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, Math.Min(row, 200));
        wb.SaveAs(output);
    }

    private static void SetText(IXLWorksheet ws, int row, int col, string? value)
    {
        var cell = ws.Cell(row, col);
        cell.Style.NumberFormat.Format = "@";
        cell.SetValue(value ?? "");
    }
}
