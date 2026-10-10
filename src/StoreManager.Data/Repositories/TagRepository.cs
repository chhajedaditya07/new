using Microsoft.EntityFrameworkCore;
using StoreManager.Core.Abstractions;
using StoreManager.Core.Entities;
using StoreManager.Core.Models;

namespace StoreManager.Data.Repositories;

public sealed class TagRepository : ITagRepository
{
    public const string Required = "Required";
    public const string Invalid = "Invalid";
    public const string Duplicate = "Duplicate";
    public const string NotFound = "NotFound";
    public const string SystemTag = "System";
    public const string SameTag = "SameTag";

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IClock _clock;
    private readonly ICurrentUser _user;

    public TagRepository(IDbContextFactory<AppDbContext> factory, IClock clock, ICurrentUser user)
    {
        _factory = factory;
        _clock = clock;
        _user = user;
    }

    public async Task<IReadOnlyList<TagInfo>> ListAsync(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.Tags.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TagInfo(t.Id, t.Name, t.ColorHex, t.IsSystem,
                ctx.ContactTags.Count(l => l.TagId == t.Id)))
            .ToListAsync(ct);
    }

    public async Task<TagResult> CreateAsync(string name, string colorHex, CancellationToken ct = default)
    {
        var check = Validate(name, colorHex, out name);
        if (check is not null) return new TagResult(false, check);

        await using var ctx = await _factory.CreateDbContextAsync(ct);
        if (await ctx.Tags.AnyAsync(t => t.Name == name, ct)) return new TagResult(false, Duplicate);

        var tag = new Tag { Name = name, ColorHex = colorHex.ToUpperInvariant() };
        ctx.Tags.Add(tag);
        Audit.Log(ctx, _clock, _user, "Create", nameof(Tag), tag.Id, name);
        await ctx.SaveChangesAsync(ct);
        return new TagResult(true, Tag: tag);
    }

    public async Task<TagResult> UpdateAsync(Guid id, string name, string colorHex, CancellationToken ct = default)
    {
        var check = Validate(name, colorHex, out name);
        if (check is not null) return new TagResult(false, check);

        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var tag = await ctx.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag is null) return new TagResult(false, NotFound);
        if (tag.IsSystem) return new TagResult(false, SystemTag);
        if (await ctx.Tags.AnyAsync(t => t.Name == name && t.Id != id, ct)) return new TagResult(false, Duplicate);

        tag.Name = name;
        tag.ColorHex = colorHex.ToUpperInvariant();
        Audit.Log(ctx, _clock, _user, "Update", nameof(Tag), id, name);
        await ctx.SaveChangesAsync(ct);
        return new TagResult(true, Tag: tag);
    }

    public async Task<TagResult> MergeAsync(Guid sourceId, Guid targetId, CancellationToken ct = default)
    {
        if (sourceId == targetId) return new TagResult(false, SameTag);

        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var source = await ctx.Tags.FirstOrDefaultAsync(t => t.Id == sourceId, ct);
        var target = await ctx.Tags.FirstOrDefaultAsync(t => t.Id == targetId, ct);
        if (source is null || target is null) return new TagResult(false, NotFound);
        if (source.IsSystem || target.IsSystem) return new TagResult(false, SystemTag);

        await using var tx = await ctx.Database.BeginTransactionAsync(ct);
        // Contacts that already carry the target tag simply lose the source link; the rest are re-pointed.
        await ctx.Database.ExecuteSqlInterpolatedAsync($@"
            DELETE FROM ContactTags WHERE TagId = {sourceId}
              AND ContactId IN (SELECT ContactId FROM ContactTags WHERE TagId = {targetId})", ct);
        await ctx.Database.ExecuteSqlInterpolatedAsync($"UPDATE ContactTags SET TagId = {targetId} WHERE TagId = {sourceId}", ct);
        await ctx.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Tags WHERE Id = {sourceId}", ct);
        Audit.Log(ctx, _clock, _user, "Merge", nameof(Tag), targetId, $"{source.Name} -> {target.Name}");
        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new TagResult(true, Tag: target);
    }

    public async Task<TagResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var tag = await ctx.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag is null) return new TagResult(false, NotFound);
        if (tag.IsSystem) return new TagResult(false, SystemTag);

        ctx.Tags.Remove(tag);   // ContactTags cascade
        Audit.Log(ctx, _clock, _user, "Delete", nameof(Tag), id, tag.Name);
        await ctx.SaveChangesAsync(ct);
        return new TagResult(true);
    }

    private static string? Validate(string name, string colorHex, out string cleanName)
    {
        cleanName = (name ?? "").Trim();
        if (cleanName.Length == 0) return Required;
        if (cleanName.Length > 60 || !TagColors.IsValid(colorHex)) return Invalid;
        return null;
    }
}
