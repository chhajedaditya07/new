using System.Text;

namespace StoreManager.Core.Formatting;

/// <summary>Money is stored as integer paise and shown with Indian digit grouping.</summary>
public static class Money
{
    /// <summary>1250000 paise =&gt; "₹12,500"; 12550 =&gt; "₹125.50"; -50000 =&gt; "-₹500".</summary>
    public static string Format(long paise, bool alwaysShowPaise = false)
    {
        var negative = paise < 0;
        var abs = negative ? -paise : paise;
        var rupees = abs / 100;
        var rem = (int)(abs % 100);

        var sb = new StringBuilder();
        if (negative) sb.Append('-');
        sb.Append('₹').Append(GroupIndian(rupees));
        if (rem != 0 || alwaysShowPaise) sb.Append('.').Append(rem.ToString("00"));
        return sb.ToString();
    }

    public static string GroupIndian(long value)
    {
        var s = value.ToString();
        if (s.Length <= 3) return s;
        var last3 = s[^3..];
        var rest = s[..^3];
        var parts = new List<string>();
        while (rest.Length > 2)
        {
            parts.Insert(0, rest[^2..]);
            rest = rest[..^2];
        }
        if (rest.Length > 0) parts.Insert(0, rest);
        return string.Join(',', parts) + "," + last3;
    }

    /// <summary>Parses "1,25,000", "₹ 500.50", "500" into paise. Returns false on garbage.</summary>
    public static bool TryParseToPaise(string? text, out long paise)
    {
        paise = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var cleaned = text.Replace("₹", "").Replace(",", "").Replace(" ", "").Trim();
        if (!decimal.TryParse(cleaned, System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture, out var d) || d < 0) return false;
        paise = (long)Math.Round(d * 100m, MidpointRounding.AwayFromZero);
        return true;
    }
}
