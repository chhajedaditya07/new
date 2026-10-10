using System.Text.RegularExpressions;

namespace StoreManager.Services.Messaging;

/// <summary>Replaces {name}, {store}, {date}… placeholders. Unknown placeholders are left untouched so typos are visible.</summary>
public static partial class TemplateRenderer
{
    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();

    public static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        var lookup = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
        return Placeholder().Replace(template, m => lookup.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
    }
}
