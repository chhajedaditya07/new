using Microsoft.EntityFrameworkCore;
using StoreManager.Core.Abstractions;
using StoreManager.Core.Entities;
using StoreManager.Data.Repositories;

namespace StoreManager.Data.Seed;

/// <summary>
/// Demo contacts are all linked to the system "Demo data" tag, so removing them is exact and never touches real records.
/// </summary>
public sealed class DemoDataService
{
    private static readonly string[] FirstNames =
    {
        "Aarti", "Sunita", "Meena", "Pooja", "Kavita", "Neha", "Rekha", "Priya", "Anita", "Sapna",
        "Rajesh", "Mahesh", "Suresh", "Dinesh", "Ramesh", "Vikram", "Amit", "Rohit", "Manoj", "Kiran"
    };
    private static readonly string[] LastNames =
    {
        "Sharma", "Gupta", "Jain", "Agarwal", "Mehta", "Patel", "Shah", "Verma", "Chhajed", "Bansal",
        "Joshi", "Khandelwal", "Mittal", "Singhal", "Rathi", "Soni", "Maheshwari", "Kothari"
    };
    private static readonly string[] Cities = { "Jaipur", "Surat", "Indore", "Udaipur", "Ahmedabad", "Kota", "Jodhpur", "Pune" };
    private static readonly (string Name, string Color)[] DemoTags =
    {
        ("VIP", "#6B4FBB"), ("Bridal 2026", "#B4009E"), ("Wholesale", "#0F6CBD"),
        ("Credit Customer", "#D97706"), ("Surat Supplier", "#008272"), ("Do Not Call", "#C42B1C")
    };

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly SettingsStore _settings;
    private readonly IClock _clock;
    private readonly ICurrentUser _user;

    public DemoDataService(IDbContextFactory<AppDbContext> factory, SettingsStore settings, IClock clock, ICurrentUser user)
    {
        _factory = factory;
        _settings = settings;
        _clock = clock;
        _user = user;
    }

    public async Task<bool> IsLoadedAsync(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.ContactTags.AnyAsync(l => l.Tag.Name == SystemTags.Demo, ct);
    }

    /// <summary>Adds <paramref name="count"/> demo contacts. Deterministic for a given seed. Returns how many were created.</summary>
    public async Task<int> LoadAsync(int count = 60, int seed = 42, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var rng = new Random(seed);

        var tags = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in await ctx.Tags.ToListAsync(ct)) tags[t.Name] = t;

        Tag Ensure(string name, string color, bool system = false)
        {
            if (tags.TryGetValue(name, out var t)) return t;
            t = new Tag { Name = name, ColorHex = color, IsSystem = system };
            tags[name] = t;
            ctx.Tags.Add(t);
            return t;
        }

        var demoTag = Ensure(SystemTags.Demo, "#4A5568", system: true);
        foreach (var (n, c) in DemoTags) Ensure(n, c);

        var used = (await ctx.Contacts.Select(c => c.Mobile).ToListAsync(ct)).ToHashSet();
        var created = 0;
        var attempts = 0;
        while (created < count && attempts++ < count * 20)
        {
            var mobile = "+91" + (rng.Next(6, 10) * 1_000_000_000L + rng.NextInt64(0, 1_000_000_000L)).ToString("D10");
            if (!used.Add(mobile)) continue;

            var type = created % 12 == 0 ? ContactType.Supplier
                     : created % 15 == 0 ? ContactType.Karigar
                     : ContactType.Customer;
            var first = FirstNames[rng.Next(FirstNames.Length)];
            var last = LastNames[rng.Next(LastNames.Length)];

            var c = new Contact
            {
                Type = type,
                Name = type == ContactType.Karigar ? $"Karigar {first} {last}" : $"{first} {last}",
                Mobile = mobile,
                City = Cities[rng.Next(Cities.Length)],
                Birthday = rng.Next(3) == 0 ? new DateOnly(1970 + rng.Next(40), rng.Next(1, 13), rng.Next(1, 28)) : null,
                Notes = type == ContactType.Customer && rng.Next(4) == 0 ? "Prefers Banarasi and Kanjivaram sarees" : null
            };
            c.ContactTags.Add(new ContactTag { ContactId = c.Id, Tag = demoTag });
            if (type == ContactType.Customer)
            {
                var pick = DemoTags[rng.Next(DemoTags.Length)].Name;
                if (pick != "Surat Supplier") c.ContactTags.Add(new ContactTag { ContactId = c.Id, Tag = tags[pick] });
            }
            else if (type == ContactType.Supplier)
            {
                c.ContactTags.Add(new ContactTag { ContactId = c.Id, Tag = tags["Surat Supplier"] });
            }
            ctx.Contacts.Add(c);
            created++;
        }

        Audit.Log(ctx, _clock, _user, "DemoLoad", nameof(Contact), null, $"{created} demo contacts");
        await ctx.SaveChangesAsync(ct);
        await _settings.SetAsync(SettingKeys.DemoDataLoaded, "1", ct);
        return created;
    }

    /// <summary>Permanently deletes only the demo contacts (those carrying the system tag).</summary>
    public async Task<int> RemoveAsync(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var ids = await ctx.Contacts.IgnoreQueryFilters()
            .Where(c => c.ContactTags.Any(l => l.Tag.Name == SystemTags.Demo))
            .Select(c => c.Id).ToListAsync(ct);

        var removed = 0;
        foreach (var chunk in ids.Chunk(500))
        {
            var part = chunk.ToList();
            removed += await ctx.Contacts.IgnoreQueryFilters().Where(c => part.Contains(c.Id)).ExecuteDeleteAsync(ct);
        }
        await ctx.Tags.Where(t => t.Name == SystemTags.Demo).ExecuteDeleteAsync(ct);
        Audit.Log(ctx, _clock, _user, "DemoRemove", nameof(Contact), null, $"{removed} demo contacts");
        await ctx.SaveChangesAsync(ct);
        await _settings.SetAsync(SettingKeys.DemoDataLoaded, "0", ct);
        return removed;
    }
}
