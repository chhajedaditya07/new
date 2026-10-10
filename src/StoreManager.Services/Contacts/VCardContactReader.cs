using System.Text;

namespace StoreManager.Services.Contacts;

/// <summary>Reads .vcf exports (phone/Google Contacts, vCard 2.1–4.0). Produces records using the same keys as the spreadsheet mapper.</summary>
public static class VCardContactReader
{
    public static IReadOnlyList<RawRecord> Read(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lines = Unfold(reader.ReadToEnd());

        var records = new List<RawRecord>();
        Dictionary<string, string>? current = null;
        var cellPhones = new List<string>();
        var otherPhones = new List<string>();
        var index = 0;

        foreach (var line in lines)
        {
            var upper = line.ToUpperInvariant();
            if (upper == "BEGIN:VCARD") { current = new(); cellPhones = new(); otherPhones = new(); index++; continue; }
            if (upper == "END:VCARD")
            {
                if (current is not null)
                {
                    // CELL numbers come first (in file order), then the rest.
                    var phones = cellPhones.Concat(otherPhones).ToList();
                    if (phones.Count > 0) current["mobile"] = phones[0];
                    if (phones.Count > 1) current["alt"] = phones[1];
                    records.Add(new RawRecord(index, current));
                }
                current = null;
                continue;
            }
            if (current is null) continue;

            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var head = line[..colon];
            var value = line[(colon + 1)..].Trim();
            var parts = head.Split(';');
            var name = parts[0];
            var dot = name.IndexOf('.');                 // "item1.TEL"
            if (dot >= 0) name = name[(dot + 1)..];
            name = name.ToUpperInvariant();

            switch (name)
            {
                case "FN":
                    if (value.Length > 0) current["name"] = Unescape(value);
                    break;
                case "N":
                    if (!current.ContainsKey("name"))
                    {
                        var n = value.Split(';');                     // family;given;additional;prefix;suffix
                        var full = $"{(n.Length > 1 ? n[1] : "")} {n[0]}".Trim();
                        if (full.Length > 0) current["name"] = Unescape(full);
                    }
                    break;
                case "TEL":
                    if (value.Length > 0)
                    {
                        var isCell = parts.Skip(1).Any(p => p.Contains("CELL", StringComparison.OrdinalIgnoreCase));
                        (isCell ? cellPhones : otherPhones).Add(value);
                    }
                    break;
                case "ADR":
                    var adr = value.Split(';');                       // pobox;ext;street;city;region;zip;country
                    if (adr.Length > 3 && adr[3].Length > 0) current["city"] = Unescape(adr[3]);
                    var street = string.Join(", ", adr.Where(a => a.Length > 0).Select(Unescape));
                    if (street.Length > 0) current["address"] = street;
                    break;
                case "BDAY":
                    current["birthday"] = NormalizeVCardDate(value);
                    break;
                case "NOTE":
                    current["notes"] = Unescape(value);
                    break;
                case "CATEGORIES":
                    current["tags"] = Unescape(value);
                    break;
            }
        }
        return records;
    }

    private static List<string> Unfold(string text)
    {
        var result = new List<string>();
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t') && result.Count > 0)
                result[^1] += raw[1..];
            else if (raw.Length > 0)
                result.Add(raw.TrimEnd());
        }
        return result;
    }

    private static string Unescape(string s) =>
        s.Replace("\\n", " ").Replace("\\N", " ").Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\");

    /// <summary>"19900115" or "1990-01-15" =&gt; "1990-01-15" (parsed later by IstDate).</summary>
    private static string NormalizeVCardDate(string v)
    {
        var d = v.Split('T')[0].Replace("-", "");
        return d.Length == 8 ? $"{d[..4]}-{d[4..6]}-{d[6..8]}" : v;
    }
}
