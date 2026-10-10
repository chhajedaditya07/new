using Microsoft.EntityFrameworkCore;
using StoreManager.Core.Abstractions;
using StoreManager.Core.Entities;
using StoreManager.Core.Formatting;
using StoreManager.Core.Models;
using StoreManager.Core.Validation;

namespace StoreManager.Data.Repositories;

public sealed class ContactRepository : IContactRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IClock _clock;
    private readonly ICurrentUser _user;

    public ContactRepository(IDbContextFactory<AppDbContext> factory, IClock clock, ICurrentUser user)
    {
        _factory = factory;
        _clock = clock;
        _user = user;
    }

    // ---------- queries ----------

    public async Task<PagedResult<ContactListItem>> SearchAsync(ContactQuery q, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var filtered = ApplyFilters(ctx.Contacts.AsNoTracking(), q);

        var total = await filtered.CountAsync(ct);
        var rows = await ApplySort(filtered, q.Sort)
            .Skip(q.Skip).Take(q.Take)
            .Select(c => new
            {
                c.Id, c.Name, c.Mobile, c.Type, c.City,
                Tags = c.ContactTags
                    .OrderBy(ct => ct.Tag.Name)
                    .Select(ct => new TagChip(ct.TagId, ct.Tag.Name, ct.Tag.ColorHex))
                    .ToList()
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new ContactListItem(r.Id, r.Name, r.Mobile, r.Type, r.City, r.Tags)).ToList();
        return new PagedResult<ContactListItem>(items, total);
    }

    public async Task<IReadOnlyList<Contact>> ListForExportAsync(ContactQuery q, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ApplySort(ApplyFilters(ctx.Contacts.AsNoTracking(), q), q.Sort)
            .Include(c => c.ContactTags).ThenInclude(ct => ct.Tag)
            .ToListAsync(ct);
    }

    public async Task<Contact?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.Contacts.AsNoTracking()
            .Include(c => c.ContactTags).ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<Contact?> FindByMobileAsync(string mobile, CancellationToken ct = default)
    {
        if (!PhoneNumber.TryNormalize(mobile, out var normalized)) return null;
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.Mobile == normalized, ct);
    }

    private static IQueryable<Contact> ApplyFilters(IQueryable<Contact> query, ContactQuery q)
    {
        if (q.Types is { Count: > 0 })
        {
            var types = q.Types.ToList();
            query = query.Where(c => types.Contains(c.Type));
        }

        if (q.TagIds is { Count: > 0 })
        {
            var tagIds = q.TagIds.ToList();
            if (q.TagMatch == TagMatchMode.All)
            {
                foreach (var tagId in tagIds)
                {
                    var id = tagId;
                    query = query.Where(c => c.ContactTags.Any(ct => ct.TagId == id));
                }
            }
            else
            {
                query = query.Where(c => c.ContactTags.Any(ct => tagIds.Contains(ct.TagId)));
            }
        }

        var text = q.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            var namePattern = $"%{EscapeLike(text)}%";
            var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
            var nonSpace = text.Count(ch => !char.IsWhiteSpace(ch) && ch != '+' && ch != '-');
            var looksLikePhone = digits.Length >= 3 && digits.Length == nonSpace;

            if (looksLikePhone)
            {
                var phonePattern = $"%{digits}%";
                query = query.Where(c =>
                    EF.Functions.Like(c.Mobile, phonePattern) ||
                    (c.AltPhone != null && EF.Functions.Like(c.AltPhone, phonePattern)) ||
                    (c.WhatsApp != null && EF.Functions.Like(c.WhatsApp, phonePattern)));
            }
            else
            {
                query = query.Where(c =>
                    EF.Functions.Like(c.Name, namePattern, "\\") ||
                    (c.City != null && EF.Functions.Like(c.City, namePattern, "\\")));
            }
        }
        return query;
    }

    private static IOrderedQueryable<Contact> ApplySort(IQueryable<Contact> query, ContactSort sort) => sort switch
    {
        ContactSort.NameDesc => query.OrderByDescending(c => c.Name).ThenBy(c => c.Id),
        ContactSort.NewestFirst => query.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id),
        ContactSort.CityAsc => query.OrderBy(c => c.City).ThenBy(c => c.Name).ThenBy(c => c.Id),
        _ => query.OrderBy(c => c.Name).ThenBy(c => c.Id)
    };

    private static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    // ---------- writes ----------

    public async Task<SaveResult> SaveAsync(Contact contact, IReadOnlyCollection<Guid> tagIds, CancellationToken ct = default)
    {
        var errors = ContactValidator.ValidateAndNormalize(contact);
        if (errors.Count > 0) return new SaveResult(false, Errors: errors);

        await using var ctx = await _factory.CreateDbContextAsync(ct);

        var duplicate = await ctx.Contacts.AsNoTracking()
            .Where(c => c.Mobile == contact.Mobile && c.Id != contact.Id)
            .Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
        if (duplicate is not null)
            return new SaveResult(false,
                Errors: new[] { new ValidationError(nameof(Contact.Mobile), SaveResult.DuplicateMobile) },
                ExistingContactId: duplicate);

        var existing = await ctx.Contacts.Include(c => c.ContactTags).FirstOrDefaultAsync(c => c.Id == contact.Id, ct);
        var isNew = existing is null;
        if (isNew)
        {
            existing = new Contact { Id = contact.Id == Guid.Empty ? Guid.CreateVersion7() : contact.Id };
            ctx.Contacts.Add(existing);
        }
        CopyFields(contact, existing!);

        // Replace the tag set, ignoring ids that no longer exist.
        var wanted = tagIds.Distinct().ToList();
        var validTagIds = await ctx.Tags.Where(t => wanted.Contains(t.Id)).Select(t => t.Id).ToListAsync(ct);
        foreach (var link in existing!.ContactTags.Where(l => !validTagIds.Contains(l.TagId)).ToList())
            existing.ContactTags.Remove(link);
        foreach (var tagId in validTagIds.Where(id => existing.ContactTags.All(l => l.TagId != id)))
            existing.ContactTags.Add(new ContactTag { ContactId = existing.Id, TagId = tagId });

        Audit.Log(ctx, _clock, _user, isNew ? "Create" : "Update", nameof(Contact), existing.Id, existing.Name);

        try
        {
            await ctx.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Lost a race with another writer on the same mobile.
            return new SaveResult(false, Errors: new[] { new ValidationError(nameof(Contact.Mobile), SaveResult.DuplicateMobile) });
        }
        return new SaveResult(true, existing);
    }

    private static void CopyFields(Contact from, Contact to)
    {
        to.Type = from.Type;
        to.Name = from.Name;
        to.Mobile = from.Mobile;
        to.WhatsApp = from.WhatsApp;
        to.AltPhone = from.AltPhone;
        to.City = from.City;
        to.Address = from.Address;
        to.Gstin = from.Gstin;
        to.Birthday = from.Birthday;
        to.Anniversary = from.Anniversary;
        to.ReferredByContactId = from.ReferredByContactId;
        to.Notes = from.Notes;
        to.PhotoAttachmentId = from.PhotoAttachmentId;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 };

    public async Task SoftDeleteAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return;
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var now = _clock.UtcNow;
        var list = ids.ToList();
        var contacts = await ctx.Contacts.Where(c => list.Contains(c.Id)).ToListAsync(ct);
        foreach (var c in contacts)
        {
            c.IsDeleted = true;
            c.DeletedAt = now;
            Audit.Log(ctx, _clock, _user, "Delete", nameof(Contact), c.Id, c.Name);
        }
        await ctx.SaveChangesAsync(ct);
    }

    public async Task<int> RestoreAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return 0;
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var list = ids.ToList();
        var contacts = await ctx.Contacts.IgnoreQueryFilters()
            .Where(c => list.Contains(c.Id) && c.IsDeleted).ToListAsync(ct);

        var restored = 0;
        foreach (var c in contacts)
        {
            var taken = await ctx.Contacts.AnyAsync(x => x.Mobile == c.Mobile && x.Id != c.Id, ct);
            if (taken) continue;
            c.IsDeleted = false;
            c.DeletedAt = null;
            Audit.Log(ctx, _clock, _user, "Restore", nameof(Contact), c.Id, c.Name);
            restored++;
        }
        await ctx.SaveChangesAsync(ct);
        return restored;
    }

    public async Task AddTagsAsync(IReadOnlyCollection<Guid> contactIds, IReadOnlyCollection<Guid> tagIds, CancellationToken ct = default)
    {
        if (contactIds.Count == 0 || tagIds.Count == 0) return;
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var cIds = contactIds.ToList();
        var tIds = tagIds.ToList();

        var validTags = await ctx.Tags.Where(t => tIds.Contains(t.Id)).Select(t => t.Id).ToListAsync(ct);
        var validContacts = await ctx.Contacts.Where(c => cIds.Contains(c.Id)).Select(c => c.Id).ToListAsync(ct);
        var existing = (await ctx.ContactTags
                .Where(l => cIds.Contains(l.ContactId) && tIds.Contains(l.TagId))
                .Select(l => new { l.ContactId, l.TagId }).ToListAsync(ct))
            .Select(x => (x.ContactId, x.TagId)).ToHashSet();

        foreach (var c in validContacts)
            foreach (var t in validTags)
                if (!existing.Contains((c, t)))
                    ctx.ContactTags.Add(new ContactTag { ContactId = c, TagId = t });

        Audit.Log(ctx, _clock, _user, "BulkTag", nameof(Contact), null, $"{validContacts.Count} contacts, {validTags.Count} tags");
        await ctx.SaveChangesAsync(ct);
    }

    public async Task RemoveTagsAsync(IReadOnlyCollection<Guid> contactIds, IReadOnlyCollection<Guid> tagIds, CancellationToken ct = default)
    {
        if (contactIds.Count == 0 || tagIds.Count == 0) return;
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var cIds = contactIds.ToList();
        var tIds = tagIds.ToList();
        var links = await ctx.ContactTags.Where(l => cIds.Contains(l.ContactId) && tIds.Contains(l.TagId)).ToListAsync(ct);
        ctx.ContactTags.RemoveRange(links);
        Audit.Log(ctx, _clock, _user, "BulkUntag", nameof(Contact), null, $"{cIds.Count} contacts, {tIds.Count} tags");
        await ctx.SaveChangesAsync(ct);
    }

    public async Task<BulkCreateResult> BulkCreateAsync(IReadOnlyList<BulkContact> rows, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        await using var tx = await ctx.Database.BeginTransactionAsync(ct);

        var knownMobiles = (await ctx.Contacts.Select(c => c.Mobile).ToListAsync(ct)).ToHashSet();
        var tagsByName = (await ctx.Tags.ToListAsync(ct))
            .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

        var skipped = new List<(int, string)>();
        var created = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            var c = rows[i].Contact;
            var errors = ContactValidator.ValidateAndNormalize(c);
            if (errors.Count > 0)
            {
                skipped.Add((i, string.Join(", ", errors.Select(e => $"{e.Field}:{e.Code}"))));
                continue;
            }
            if (!knownMobiles.Add(c.Mobile))
            {
                skipped.Add((i, $"{nameof(Contact.Mobile)}:{SaveResult.DuplicateMobile}"));
                continue;
            }

            c.Id = Guid.CreateVersion7();
            foreach (var name in rows[i].TagNames.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!tagsByName.TryGetValue(name, out var tag))
                {
                    tag = new Tag { Name = name, ColorHex = TagColors.ForName(name) };
                    tagsByName[name] = tag;
                    ctx.Tags.Add(tag);
                }
                c.ContactTags.Add(new ContactTag { ContactId = c.Id, Tag = tag });
            }
            ctx.Contacts.Add(c);
            created++;

            if (created % 1000 == 0) await ctx.SaveChangesAsync(ct);
        }

        Audit.Log(ctx, _clock, _user, "Import", nameof(Contact), null, $"{created} created, {skipped.Count} skipped");
        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new BulkCreateResult(created, skipped);
    }
}
