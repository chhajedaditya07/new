using System.Globalization;

namespace StoreManager.Core.Formatting;

/// <summary>All times are stored in UTC and displayed in IST (UTC+5:30, no DST).</summary>
public static class IstDate
{
    public static readonly TimeSpan Offset = TimeSpan.FromMinutes(330);

    public static DateTime ToIst(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(Offset);

    public static DateTime FromIst(DateTime ist) =>
        DateTime.SpecifyKind(ist - Offset, DateTimeKind.Utc);

    public static DateOnly TodayIst(DateTime utcNow) => DateOnly.FromDateTime(ToIst(utcNow));

    public static string Date(DateOnly d) => d.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
    public static string Date(DateOnly? d) => d is null ? "" : Date(d.Value);
    public static string DateTimeText(DateTime utc) =>
        ToIst(utc).ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);

    private static readonly string[] Formats =
    {
        "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "yyyy-MM-dd", "yyyyMMdd", "dd-MMM-yyyy", "d MMM yyyy"
    };

    /// <summary>Accepts DD-MM-YYYY (preferred), DD/MM/YYYY, ISO and a few common variants.</summary>
    public static bool TryParse(string? text, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return DateOnly.TryParseExact(text.Trim(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
