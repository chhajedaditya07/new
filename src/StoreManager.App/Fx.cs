using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace StoreManager.App;

/// <summary>Small static helpers used from x:Bind function bindings.</summary>
public static class Fx
{
    private static readonly Dictionary<string, SolidColorBrush> BrushCache = new();
    private static readonly SolidColorBrush White = new(Colors.White);
    private static readonly SolidColorBrush Black = new(Colors.Black);

    public static Visibility Vis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility NotVis(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
    public static bool Not(bool value) => !value;

    public static SolidColorBrush Brush(string? hex)
    {
        hex ??= "#808080";
        if (!BrushCache.TryGetValue(hex, out var brush))
            BrushCache[hex] = brush = new SolidColorBrush(Parse(hex));
        return brush;
    }

    /// <summary>White or black text, whichever reads better on the tag colour.</summary>
    public static SolidColorBrush Contrast(string? hex)
    {
        var c = Parse(hex ?? "#808080");
        var luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
        return luminance > 0.6 ? Black : White;
    }

    private static Windows.UI.Color Parse(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length != 6) return Colors.Gray;
        return ColorHelper.FromArgb(255,
            Convert.ToByte(h[..2], 16), Convert.ToByte(h[2..4], 16), Convert.ToByte(h[4..6], 16));
    }
}
