using Microsoft.EntityFrameworkCore;
using StoreManager.Core.Entities;
using StoreManager.Core.Models;

namespace StoreManager.Tests.Data;

public class ContactRepositoryTests : IAsyncLifetime
{
    private TestDb _db = null!;
    public async Task InitializeAsync() => _db = await TestDb.CreateAsync();
    public Task DisposeAsync() { _db.Dispose(); return Task.CompletedTask; }

    private static Contact C(string name, string mobile, ContactType type = ContactType.Customer, string? city = null) =>
        new() { Name = name, Mobile = mobile, Type = type, City = city };

    private async Task<Contact> Add(string name, string mobile, ContactType type = ContactType.Customer,
        string? city = null, params Guid[] tags)
    {
        var r = await _db.Contacts.SaveAsync(C(name, mobile, type, city), tags);
        Assert.True(r.Success, string.Join(",", r.Errors?.Select(e => e.Field + e.Code) ?? []));
        return r.Contact!;
    }

    private async Task<Guid> Tag(string name) => (await _db.Tags.CreateAsync(name, "#0F6CBD")).Tag!.Id;

    [Fact]
    public async Task Create_stores_normalized_mobile_and_audit_fields()
    {
        var c = await Add("Aarti Sharma", "98765 43210");
        var loaded = await _db.Contacts.GetAsync(c.Id);
        Assert.Equal("+919876543210", loaded!.Mobile);
        Assert.NotEqual(default, loaded.CreatedAt);
        Assert.NotNull(loaded.CreatedBy);   // the seeded Owner
    }

    [Fact]
    public async Task Invalid_input_returns_field_errors_and_saves_nothing()
    {
        var r = await _db.Contacts.SaveAsync(C("", "12"), []);
        Assert.False(r.Success);
        Assert.Contains(r.Errors!, e => e.Field == "Name");
        Assert.Contains(r.Errors!, e => e.Field == "Mobile");
        Assert.Equal(0, (await _db.Contacts.SearchAsync(new ContactQuery())).TotalCount);
    }

    [Fact]
    public async Task Duplicate_mobile_is_rejected_and_points_at_existing_contact()
    {
        var first = await Add("Aarti", "9876543210");
        var r = await _db.Contacts.SaveAsync(C("Someone else", "+91 98765-43210"), []);
        Assert.False(r.Success);
        Assert.Equal(first.Id, r.ExistingContactId);
        Assert.Contains(r.Errors!, e => e.Field == "Mobile" && e.Code == SaveResult.DuplicateMobile);
    }

    [Fact]
    public async Task Update_changes_fields_and_keeps_own_mobile()
    {
        var c = await Add("Aarti", "9876543210");
        var edit = new Contact { Id = c.Id, Name = "Aarti Jain", Mobile = "9876543210", City = "Surat" };
        var r = await _db.Contacts.SaveAsync(edit, []);
        Assert.True(r.Success);
        var loaded = await _db.Contacts.GetAsync(c.Id);
        Assert.Equal("Aarti Jain", loaded!.Name);
        Assert.Equal("Surat", loaded.City);
        Assert.Equal(1, (await _db.Contacts.SearchAsync(new ContactQuery())).TotalCount);
    }

    [Fact]
    public async Task Update_preserves_created_audit_fields()
    {
        var c = await Add("Aarti", "9876543210");
        var created = (await _db.Contacts.GetAsync(c.Id))!.CreatedAt;
        await Task.Delay(20);
        await _db.Contacts.SaveAsync(new Contact { Id = c.Id, Name = "Aarti 2", Mobile = "9876543210" }, []);
        var after = (await _db.Contacts.GetAsync(c.Id))!;
        Assert.Equal(created, after.CreatedAt);
        Assert.True(after.UpdatedAt > created);
    }

    [Fact]
    public async Task FindByMobile_accepts_any_typed_format()
    {
        var c = await Add("Aarti", "9876543210");
        Assert.Equal(c.Id, (await _db.Contacts.FindByMobileAsync("098765 43210"))!.Id);
        Assert.Null(await _db.Contacts.FindByMobileAsync("9123456789"));
        Assert.Null(await _db.Contacts.FindByMobileAsync("garbage"));
    }

    [Fact]
    public async Task Soft_delete_hides_contact_frees_the_mobile_and_can_be_restored()
    {
        var c = await Add("Aarti", "9876543210");
        await _db.Contacts.SoftDeleteAsync([c.Id]);

        Assert.Equal(0, (await _db.Contacts.SearchAsync(new ContactQuery())).TotalCount);
        Assert.Null(await _db.Contacts.GetAsync(c.Id));

        Assert.Equal(1, await _db.Contacts.RestoreAsync([c.Id]));
        Assert.Equal(1, (await _db.Contacts.SearchAsync(new ContactQuery())).TotalCount);
    }

    [Fact]
    public async Task Restore_is_skipped_when_the_mobile_was_reused_in_the_meantime()
    {
        var old = await Add("Old owner", "9876543210");
        await _db.Contacts.SoftDeleteAsync([old.Id]);
        await Add("New owner", "9876543210");           // allowed: the old row is deleted

        Assert.Equal(0, await _db.Contacts.RestoreAsync([old.Id]));
        var all = await _db.Contacts.SearchAsync(new ContactQuery());
        Assert.Equal("New owner", Assert.Single(all.Items).Name);
    }

    [Fact]
    public async Task Search_by_name_city_and_phone_fragment()
    {
        await Add("Aarti Sharma", "9876543210", city: "Jaipur");
        await Add("Meena Gupta", "9123456780", city: "Surat");
        await Add("Rekha Jain", "9000011111", city: "Surat");

        Assert.Equal(["Aarti Sharma"], Names(await _db.Contacts.SearchAsync(new ContactQuery("aart"))));      // case-insensitive
        Assert.Equal(["Meena Gupta", "Rekha Jain"], Names(await _db.Contacts.SearchAsync(new ContactQuery("surat"))));
        Assert.Equal(["Aarti Sharma"], Names(await _db.Contacts.SearchAsync(new ContactQuery("98765"))));     // phone fragment
        Assert.Equal(["Aarti Sharma"], Names(await _db.Contacts.SearchAsync(new ContactQuery("+91 98765 43210"))));
        Assert.Empty((await _db.Contacts.SearchAsync(new ContactQuery("zzz"))).Items);
    }

    [Fact]
    public async Task Search_treats_percent_and_underscore_literally()
    {
        await Add("Aarti", "9876543210");
        Assert.Empty((await _db.Contacts.SearchAsync(new ContactQuery("%"))).Items);
        Assert.Empty((await _db.Contacts.SearchAsync(new ContactQuery("A_rti"))).Items);
    }

    [Fact]
    public async Task Filter_by_type()
    {
        await Add("Cust", "9876543210");
        await Add("Supp", "9876543211", ContactType.Supplier);
        await Add("Kar", "9876543212", ContactType.Karigar);
        var r = await _db.Contacts.SearchAsync(new ContactQuery(Types: [ContactType.Supplier, ContactType.Karigar]));
        Assert.Equal(["Kar", "Supp"], Names(r));
    }

    [Fact]
    public async Task Tag_filter_supports_any_and_all()
    {
        var vip = await Tag("VIP");
        var bridal = await Tag("Bridal 2026");
        await Add("OnlyVip", "9876543210", tags: [vip]);
        await Add("OnlyBridal", "9876543211", tags: [bridal]);
        await Add("Both", "9876543212", tags: [vip, bridal]);
        await Add("None", "9876543213");

        var any = await _db.Contacts.SearchAsync(new ContactQuery(TagIds: [vip, bridal], TagMatch: TagMatchMode.Any));
        Assert.Equal(["Both", "OnlyBridal", "OnlyVip"], Names(any));

        var all = await _db.Contacts.SearchAsync(new ContactQuery(TagIds: [vip, bridal], TagMatch: TagMatchMode.All));
        Assert.Equal(["Both"], Names(all));
        Assert.Equal(2, all.Items[0].Tags.Count);
    }

    [Fact]
    public async Task Paging_returns_total_and_stable_pages()
    {
        for (var i = 0; i < 25; i++) await Add($"Person {i:00}", $"98765432{i:00}");
        var p1 = await _db.Contacts.SearchAsync(new ContactQuery(Skip: 0, Take: 10));
        var p3 = await _db.Contacts.SearchAsync(new ContactQuery(Skip: 20, Take: 10));
        Assert.Equal(25, p1.TotalCount);
        Assert.Equal(10, p1.Items.Count);
        Assert.Equal(5, p3.Items.Count);
        Assert.Equal("Person 00", p1.Items[0].Name);
        Assert.Equal("Person 24", p3.Items[^1].Name);
    }

    [Fact]
    public async Task Sorting_options()
    {
        await Add("Bravo", "9876543210", city: "Pune");
        await Add("Alpha", "9876543211", city: "Surat");
        await Task.Delay(15);
        await Add("Charlie", "9876543212", city: "Agra");
        Assert.Equal(["Charlie", "Bravo", "Alpha"], Names(await _db.Contacts.SearchAsync(new ContactQuery(Sort: ContactSort.NameDesc))));
        Assert.Equal("Charlie", (await _db.Contacts.SearchAsync(new ContactQuery(Sort: ContactSort.NewestFirst))).Items[0].Name);
        Assert.Equal("Charlie", (await _db.Contacts.SearchAsync(new ContactQuery(Sort: ContactSort.CityAsc))).Items[0].Name);
    }

    [Fact]
    public async Task Bulk_tag_and_untag()
    {
        var vip = await Tag("VIP");
        var a = await Add("A", "9876543210");
        var b = await Add("B", "9876543211");

        await _db.Contacts.AddTagsAsync([a.Id, b.Id], [vip]);
        await _db.Contacts.AddTagsAsync([a.Id, b.Id], [vip]);          // idempotent
        Assert.Equal(2, (await _db.Contacts.SearchAsync(new ContactQuery(TagIds: [vip]))).TotalCount);

        await _db.Contacts.RemoveTagsAsync([a.Id], [vip]);
        Assert.Equal(["B"], Names(await _db.Contacts.SearchAsync(new ContactQuery(TagIds: [vip]))));
    }

    [Fact]
    public async Task Saving_replaces_the_tag_set()
    {
        var vip = await Tag("VIP");
        var wholesale = await Tag("Wholesale");
        var c = await Add("A", "9876543210", tags: [vip]);
        await _db.Contacts.SaveAsync(new Contact { Id = c.Id, Name = "A", Mobile = "9876543210" }, [wholesale]);
        var tags = (await _db.Contacts.GetAsync(c.Id))!.ContactTags.Select(t => t.Tag.Name).ToList();
        Assert.Equal(["Wholesale"], tags);
    }

    [Fact]
    public async Task Bulk_create_skips_bad_and_duplicate_rows_and_creates_tags()
    {
        await Add("Existing", "9876543210");
        var rows = new List<BulkContact>
        {
            new(C("Fresh", "9123456780"), ["Imported", "VIP"]),
            new(C("Dup of existing", "9876543210"), []),
            new(C("Bad number", "12"), []),
            new(C("Dup inside batch", "9123456780"), []),
        };
        var result = await _db.Contacts.BulkCreateAsync(rows);
        Assert.Equal(1, result.Created);
        Assert.Equal([1, 2, 3], result.Skipped.Select(s => s.Index).Order().ToArray());

        var tags = await _db.Tags.ListAsync();
        Assert.Equal(["Imported", "VIP"], tags.Select(t => t.Name).Order().ToArray());
    }

    [Fact]
    public async Task Activity_log_records_changes()
    {
        var c = await Add("A", "9876543210");
        await _db.Contacts.SoftDeleteAsync([c.Id]);
        await using var ctx = await _db.Factory.CreateDbContextAsync();
        var actions = await ctx.ActivityLogs.Where(a => a.EntityId == c.Id).Select(a => a.Action).ToListAsync();
        Assert.Contains("Create", actions);
        Assert.Contains("Delete", actions);
    }

    private static string[] Names(PagedResult<ContactListItem> r) => r.Items.Select(i => i.Name).ToArray();
}
