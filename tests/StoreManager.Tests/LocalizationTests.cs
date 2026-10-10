using System.Text.RegularExpressions;
using StoreManager.Tests.ViewModels;

namespace StoreManager.Tests;

/// <summary>Guards the bilingual requirement: every string the UI uses exists in English and Hindi.</summary>
public class LocalizationTests
{
    private static readonly string Root = RepoRoot.Path;
    private static readonly Dictionary<string, string> En = ReswLocalizer.Load("en-US");
    private static readonly Dictionary<string, string> Hi = ReswLocalizer.Load("hi-IN");

    [Fact]
    public void English_and_Hindi_define_exactly_the_same_keys()
    {
        var onlyEn = En.Keys.Except(Hi.Keys).ToList();
        var onlyHi = Hi.Keys.Except(En.Keys).ToList();
        Assert.True(onlyEn.Count == 0 && onlyHi.Count == 0, $"Only English: {string.Join(",", onlyEn)} | Only Hindi: {string.Join(",", onlyHi)}");
    }

    [Fact]
    public void No_translation_is_blank()
    {
        Assert.All(En, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"English '{kv.Key}' is blank"));
        Assert.All(Hi, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"Hindi '{kv.Key}' is blank"));
    }

    [Fact]
    public void Hindi_strings_keep_the_same_placeholders_as_English()
    {
        foreach (var (key, en) in En)
        {
            var enHoles = Regex.Matches(en, @"\{\d+\}").Select(m => m.Value).Order().ToArray();
            var hiHoles = Regex.Matches(Hi[key], @"\{\d+\}").Select(m => m.Value).Order().ToArray();
            Assert.True(enHoles.SequenceEqual(hiHoles), $"Placeholders differ for '{key}'");
        }
    }

    [Fact]
    public void Hindi_text_is_actually_translated_where_it_should_be()
    {
        var devanagari = new Regex(@"[ऀ-ॿ]");
        var untranslated = Hi.Where(kv => !devanagari.IsMatch(kv.Value) && kv.Value == En[kv.Key])
            .Select(kv => kv.Key)
            // Brand names, codes and numeric/format-only strings are intentionally identical.
            .Where(k => !new[] { "WhatsApp", "GSTIN", "DD-MM-YYYY", "+91 98765 43210" }.Contains(En[k]))
            .ToList();
        Assert.Empty(untranslated);
    }

    [Fact]
    public void Every_x_Uid_in_xaml_has_a_resource()
    {
        var uids = Directory.GetFiles(Path.Combine(Root, "src", "StoreManager.App"), "*.xaml", SearchOption.AllDirectories)
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "x:Uid=\"([^\"]+)\"").Select(m => m.Groups[1].Value))
            .Distinct().ToList();
        Assert.NotEmpty(uids);
        var missing = uids.Where(u => !En.Keys.Any(k => k.StartsWith(u + "."))).ToList();
        Assert.True(missing.Count == 0, "x:Uid without resources: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_resource_key_used_from_code_exists()
    {
        var sources = new[] { "StoreManager.ViewModels", "StoreManager.App" }
            .SelectMany(p => Directory.GetFiles(Path.Combine(Root, "src", p), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();

        var literal = new Regex(@"\b(?:_?loc|_loc)\.(?:Get|Format)\(\s*""([^""]+)""");
        var keys = sources.SelectMany(f => literal.Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            // keys returned by Messages.KeyFor / TagError
            .Concat(Regex.Matches(File.ReadAllText(Path.Combine(Root, "src", "StoreManager.ViewModels", "Infrastructure", "Messages.cs")), "\"(Err_\\w+)\"").Select(m => m.Groups[1].Value))
            .Distinct().ToList();

        Assert.True(keys.Count > 40, $"expected to find many keys, found {keys.Count}");
        // Keys ending in '_' are prefixes completed at run time; Dynamic_key_families_are_complete covers them.
        var missing = keys.Where(k => !k.EndsWith('_') && !En.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, "Missing resource keys: " + string.Join(", ", missing));
    }

    [Fact]
    public void Dynamic_key_families_are_complete()
    {
        foreach (var t in Enum.GetNames<StoreManager.Core.Entities.ContactType>()) Assert.Contains($"ContactType_{t}", En.Keys);
        foreach (var tag in new[] { "tasks", "followups", "payments", "fallpico", "reports" })
        {
            Assert.Contains($"Nav_Title_{tag}", En.Keys);
            Assert.Contains($"ComingSoon_{tag}", En.Keys);
        }
    }
}
