namespace StoreManager.Core.Formatting;

public static class PhoneNumber
{
    /// <summary>
    /// Normalises Indian mobile numbers to "+91XXXXXXXXXX". Accepts "98765 43210", "09876543210",
    /// "+91-98765-43210", "919876543210". Other international numbers ("+44 …") are kept as "+digits".
    /// </summary>
    public static bool TryNormalize(string? raw, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var trimmed = raw.Trim();
        var hasPlus = trimmed.StartsWith('+') || trimmed.StartsWith("00");
        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());
        if (trimmed.StartsWith("00")) digits = digits[2..];
        if (digits.Length == 0) return false;

        if (hasPlus && !digits.StartsWith("91"))
        {
            if (digits.Length is < 8 or > 15) return false;
            normalized = "+" + digits;
            return true;
        }

        if (digits.Length == 12 && digits.StartsWith("91")) digits = digits[2..];
        else if (digits.Length == 11 && digits.StartsWith('0')) digits = digits[1..];

        if (digits.Length != 10 || digits[0] is < '6' or > '9') return false;
        normalized = "+91" + digits;
        return true;
    }

    /// <summary>"+919876543210" =&gt; "+91 98765 43210".</summary>
    public static string Display(string? normalized)
    {
        if (string.IsNullOrEmpty(normalized)) return "";
        if (normalized.StartsWith("+91") && normalized.Length == 13)
            return $"+91 {normalized.Substring(3, 5)} {normalized.Substring(8, 5)}";
        return normalized;
    }

    /// <summary>Digits only, as wa.me expects ("919876543210").</summary>
    public static string ToWhatsAppDigits(string? normalized) =>
        new((normalized ?? "").Where(char.IsAsciiDigit).ToArray());
}
