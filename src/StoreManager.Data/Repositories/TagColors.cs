using System.Text.RegularExpressions;

namespace StoreManager.Data.Repositories;

public static partial class TagColors
{
    /// <summary>Palette chosen for readable contrast with either white or black text.</summary>
    public static readonly string[] Palette =
    {
        "#C42B1C", "#D97706", "#107C10", "#0F6CBD", "#6B4FBB",
        "#B4009E", "#008272", "#8E562E", "#5B5FC7", "#4A5568"
    };

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexPattern();

    public static bool IsValid(string? hex) => hex is not null && HexPattern().IsMatch(hex);

    /// <summary>Stable colour for auto-created tags so the same name always looks the same.</summary>
    public static string ForName(string name)
    {
        var hash = 17;
        foreach (var ch in name.ToLowerInvariant()) hash = unchecked(hash * 31 + ch);
        return Palette[(hash & 0x7fffffff) % Palette.Length];
    }
}
