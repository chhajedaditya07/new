using StoreManager.Core.Entities;
using StoreManager.Core.Formatting;
using StoreManager.Core.Validation;

namespace StoreManager.Services.Contacts;

/// <summary>Maps loosely-named spreadsheet columns onto Contact fields and validates each row.</summary>
public static class ContactRecordMapper
{
    private static readonly Dictionary<string, string> HeaderSynonyms = Build(new()
    {
        ["name"] = new[] { "name", "fullname", "customername", "contactname", "party", "partyname" },
        ["mobile"] = new[] { "mobile", "mobileno", "mobilenumber", "phone", "phoneno", "phonenumber", "contactno", "contactnumber", "number", "cell", "tel" },
        ["whatsapp"] = new[] { "whatsapp", "whatsappno", "whatsappnumber" },
        ["alt"] = new[] { "alt", "altphone", "altnumber", "alternate", "alternatenumber", "alternatephone", "phone2", "mobile2" },
        ["city"] = new[] { "city", "area", "areacity", "location", "town" },
        ["address"] = new[] { "address", "addr" },
        ["gstin"] = new[] { "gstin", "gst", "gstno", "gstnumber" },
        ["birthday"] = new[] { "birthday", "dob", "birthdate", "dateofbirth" },
        ["anniversary"] = new[] { "anniversary", "anniversarydate" },
        ["notes"] = new[] { "notes", "note", "remarks", "comment", "comments" },
        ["tags"] = new[] { "tags", "tag", "labels", "groups", "categories" },
        ["type"] = new[] { "type", "contacttype", "category" },
    });

    private static Dictionary<string, string> Build(Dictionary<string, string[]> map)
    {
        var d = new Dictionary<string, string>();
        foreach (var (canonical, synonyms) in map)
            foreach (var s in synonyms) d.TryAdd(s, canonical);
        return d;
    }

    /// <summary>"Mobile No." =&gt; "mobileno".</summary>
    public static string NormalizeHeader(string header) =>
        new(header.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>Converts a raw header to a canonical field name ("mobile", "city"…) or null if unknown.</summary>
    public static string? Canonical(string header) =>
        HeaderSynonyms.TryGetValue(NormalizeHeader(header), out var c) ? c : null;

    public static ImportPreview Map(IEnumerable<RawRecord> records)
    {
        var valid = new List<ContactImportRow>();
        var issues = new List<ImportIssue>();
        var seenMobiles = new HashSet<string>();

        foreach (var rec in records)
        {
            var fields = new Dictionary<string, string>();
            foreach (var (header, value) in rec.Values)
                if (Canonical(header) is { } canon && !string.IsNullOrWhiteSpace(value))
                    fields.TryAdd(canon, value.Trim());

            if (fields.Count == 0) continue;   // blank row

            string Get(string key) => fields.TryGetValue(key, out var v) ? v : "";

            var contact = new Contact
            {
                Name = Get("name"),
                Mobile = Get("mobile"),
                WhatsApp = NullIfEmpty(Get("whatsapp")),
                AltPhone = NullIfEmpty(Get("alt")),
                City = NullIfEmpty(Get("city")),
                Address = NullIfEmpty(Get("address")),
                Gstin = NullIfEmpty(Get("gstin")),
                Notes = NullIfEmpty(Get("notes")),
                Type = ParseType(Get("type")),
            };

            var rowIssues = new List<string>();
            if (fields.ContainsKey("birthday"))
            {
                if (IstDate.TryParse(fields["birthday"], out var b)) contact.Birthday = b;
                else rowIssues.Add($"Birthday '{fields["birthday"]}' is not a valid date (use DD-MM-YYYY)");
            }
            if (fields.ContainsKey("anniversary"))
            {
                if (IstDate.TryParse(fields["anniversary"], out var a)) contact.Anniversary = a;
                else rowIssues.Add($"Anniversary '{fields["anniversary"]}' is not a valid date (use DD-MM-YYYY)");
            }

            foreach (var e in ContactValidator.ValidateAndNormalize(contact))
                rowIssues.Add($"{e.Field}: {(e.Code == ContactValidator.Required ? "required" : "invalid")}");

            if (rowIssues.Count == 0 && !seenMobiles.Add(contact.Mobile))
                rowIssues.Add("Duplicate mobile within the file");

            if (rowIssues.Count > 0)
            {
                issues.Add(new ImportIssue(rec.RowNumber, string.Join("; ", rowIssues)));
                continue;
            }

            var tags = Get("tags").Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            valid.Add(new ContactImportRow(rec.RowNumber, contact, tags));
        }
        return new ImportPreview(valid, issues);
    }

    private static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;

    private static ContactType ParseType(string s) =>
        Enum.TryParse<ContactType>(s, ignoreCase: true, out var t) && Enum.IsDefined(t) ? t : ContactType.Customer;
}
