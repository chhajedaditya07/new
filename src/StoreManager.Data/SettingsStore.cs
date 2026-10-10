using Microsoft.EntityFrameworkCore;
using StoreManager.Core.Entities;

namespace StoreManager.Data;

public sealed class SettingsStore
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SettingsStore(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.Settings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);
    }

    public async Task<string> GetAsync(string key, string fallback, CancellationToken ct = default) =>
        await GetAsync(key, ct) ?? fallback;

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var row = await ctx.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null) ctx.Settings.Add(new Setting { Key = key, Value = value });
        else row.Value = value;
        await ctx.SaveChangesAsync(ct);
    }
}
