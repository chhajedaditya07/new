using System.Text;
using ClosedXML.Excel;
using StoreManager.Core.Entities;
using StoreManager.Core.Models;
using StoreManager.Services.Contacts;

namespace StoreManager.Tests.Services;

public class ImportExportTests
{
    private static MemoryStream Utf8(string s) => new(new UTF8Encoding(true).GetBytes(s));

    [Theory]
    [InlineData("Mobile No.", "mobile")]
    [InlineData("PHONE", "mobile")]
    [InlineData("Customer Name", "name")]
    [InlineData("Area / City", "city")]
    [InlineData("DOB", "birthday")]
    [InlineData("GST No", "gstin")]
    [InlineData("Favourite colour", null)]
    public void Header_synonyms(string header, string? canonical) =>
        Assert.Equal(canonical, ContactRecordMapper.Canonical(header));

    [Fact]
    public void Csv_import_maps_validates_and_reports_problems_by_row()
    {
        var csv = "Name,Mobile No,City,DOB,Tags,Type\n" +
                  "Aarti Sharma,98765 43210,Jaipur,15-08-1990,\"VIP, Bridal 2026\",Customer\n" +
                  "No Mobile,,Surat,,,\n" +
                  "Bad Phone,12345,Surat,,,\n" +
                  "Bad Date,9123456780,Surat,31-31-2000,,\n" +
                  "Dup Inside,98765-43210,Pune,,,\n" +
                  "Rajesh Supplier,9000011111,Surat,,Surat Supplier,Supplier\n";
        var preview = new ContactImportService(null!).Preview(Utf8(csv), ImportFormat.Csv);

        Assert.Equal(["Aarti Sharma", "Rajesh Supplier"], preview.ValidRows.Select(r => r.Contact.Name).ToArray());
        var aarti = preview.ValidRows[0];
        Assert.Equal("+919876543210", aarti.Contact.Mobile);
        Assert.Equal(new DateOnly(1990, 8, 15), aarti.Contact.Birthday);
        Assert.Equal(["VIP", "Bridal 2026"], aarti.Tags);
        Assert.Equal(ContactType.Supplier, preview.ValidRows[1].Contact.Type);

        Assert.Equal(4, preview.Issues.Count);
        Assert.Contains(preview.Issues, i => i.Message.Contains("Mobile") && i.Message.Contains("required"));
        Assert.Contains(preview.Issues, i => i.Message.Contains("Birthday"));
        Assert.Contains(preview.Issues, i => i.Message.Contains("Duplicate mobile within the file"));
    }

    [Fact]
    public void Csv_with_semicolons_and_blank_lines_is_handled()
    {
        var csv = "Name;Mobile\nAarti;9876543210\n;\nMeena;9123456780\n";
        var preview = new ContactImportService(null!).Preview(Utf8(csv), ImportFormat.Csv);
        Assert.Equal(2, preview.ValidRows.Count);
        Assert.Empty(preview.Issues);
    }

    [Fact]
    public void Excel_numeric_phone_cells_and_date_cells_are_read_correctly()
    {
        using var ms = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("Sheet1");
            ws.Cell(1, 1).Value = "Name"; ws.Cell(1, 2).Value = "Mobile"; ws.Cell(1, 3).Value = "Birthday";
            ws.Cell(2, 1).Value = "Aarti"; ws.Cell(2, 2).Value = 9876543210L; ws.Cell(2, 3).Value = new DateTime(1990, 8, 15);
            wb.SaveAs(ms);
        }
        ms.Position = 0;
        var preview = new ContactImportService(null!).Preview(ms, ImportFormat.Excel);
        var row = Assert.Single(preview.ValidRows);
        Assert.Equal("+919876543210", row.Contact.Mobile);
        Assert.Equal(new DateOnly(1990, 8, 15), row.Contact.Birthday);
    }

    [Fact]
    public void VCard_import_handles_folding_multiple_phones_and_groups()
    {
        var vcf = "BEGIN:VCARD\r\nVERSION:3.0\r\nFN:Aarti Sharma\r\nN:Sharma;Aarti;;;\r\n" +
                  "TEL;TYPE=HOME:0141-2345678\r\nitem1.TEL;TYPE=CELL:+91 98765 43210\r\nTEL;TYPE=CELL:98111 22222\r\n" +
                  "ADR;TYPE=HOME:;;12 MI Road;Jaipur;RJ;302001;India\r\nBDAY:19900815\r\n" +
                  "NOTE:Likes\r\n  Banarasi\r\nCATEGORIES:VIP,Bridal\r\nEND:VCARD\r\n" +
                  "BEGIN:VCARD\r\nVERSION:3.0\r\nN:Gupta;Meena;;;\r\nTEL:9123456780\r\nEND:VCARD\r\n";
        var preview = new ContactImportService(null!).Preview(Utf8(vcf), ImportFormat.VCard);

        Assert.Equal(2, preview.ValidRows.Count);
        var a = preview.ValidRows[0].Contact;
        Assert.Equal("Aarti Sharma", a.Name);
        Assert.Equal("+919876543210", a.Mobile);          // first CELL number wins
        Assert.Equal("Jaipur", a.City);
        Assert.Equal(new DateOnly(1990, 8, 15), a.Birthday);
        Assert.Equal(["VIP", "Bridal"], preview.ValidRows[0].Tags);
        Assert.Equal("Meena Gupta", preview.ValidRows[1].Contact.Name);   // built from N when FN is missing
    }

    [Fact]
    public async Task Export_then_import_round_trips_through_the_database()
    {
        using var db = await StoreManager.Tests.TestDb.CreateAsync();
        var vip = (await db.Tags.CreateAsync("VIP", "#6B4FBB")).Tag!;
        await db.Contacts.SaveAsync(new Contact
        {
            Name = "Aarti Sharma", Mobile = "9876543210", City = "Jaipur", Gstin = "24AAACC1206D1ZM",
            Birthday = new DateOnly(1990, 8, 15), Notes = "Prefers silk"
        }, [vip.Id]);
        await db.Contacts.SaveAsync(new Contact { Name = "Rajesh", Mobile = "9123456780", Type = ContactType.Supplier }, []);

        var export = await db.Contacts.ListForExportAsync(new ContactQuery(Take: int.MaxValue));
        using var ms = new MemoryStream();
        ContactExportService.WriteExcel(export, ms);

        // Import into a fresh database.
        using var db2 = await StoreManager.Tests.TestDb.CreateAsync();
        var importer = new ContactImportService(db2.Contacts);
        ms.Position = 0;
        var preview = importer.Preview(ms, ImportFormat.Excel);
        Assert.Empty(preview.Issues);
        var outcome = await importer.ImportAsync(preview);
        Assert.Equal(2, outcome.Created);

        var aarti = (await db2.Contacts.FindByMobileAsync("9876543210"))!;
        var full = (await db2.Contacts.GetAsync(aarti.Id))!;
        Assert.Equal("Jaipur", full.City);
        Assert.Equal("24AAACC1206D1ZM", full.Gstin);
        Assert.Equal(new DateOnly(1990, 8, 15), full.Birthday);
        Assert.Equal(["VIP"], full.ContactTags.Select(t => t.Tag.Name).ToArray());
        Assert.Equal(ContactType.Supplier, (await db2.Contacts.FindByMobileAsync("9123456780"))!.Type);

        // Importing the same file again creates nothing and reports the duplicates.
        ms.Position = 0;
        var again = await importer.ImportAsync(importer.Preview(ms, ImportFormat.Excel));
        Assert.Equal(0, again.Created);
        Assert.Equal(2, again.Issues.Count(i => i.Message.Contains("already exists")));
    }

    [Theory]
    [InlineData("a.csv", ImportFormat.Csv)]
    [InlineData("b.XLSX", ImportFormat.Excel)]
    [InlineData("c.vcf", ImportFormat.VCard)]
    public void Detects_format(string file, ImportFormat expected) =>
        Assert.Equal(expected, ContactImportService.DetectFormat(file));

    [Fact]
    public void Unknown_format_is_null() => Assert.Null(ContactImportService.DetectFormat("x.pdf"));
}
