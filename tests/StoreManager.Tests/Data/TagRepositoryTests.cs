using StoreManager.Core.Entities;
using StoreManager.Core.Models;
using StoreManager.Data.Repositories;

namespace StoreManager.Tests.Data;

public class TagRepositoryTests : IAsyncLifetime
{
    private TestDb _db = null!;
    public async Task InitializeAsync() => _db = await TestDb.CreateAsync();
    public Task DisposeAsync() { _db.Dispose(); return Task.CompletedTask; }

    private async Task<Contact> Contact(string mobile, params Guid[] tags) =>
        (await _db.Contacts.SaveAsync(new Contact { Name = "C " + mobile, Mobile = mobile }, tags)).Contact!;

    [Fact]
    public async Task Create_trims_and_rejects_duplicates_ignoring_case()
    {
        Assert.True((await _db.Tags.CreateAsync("  VIP ", "#6b4fbb")).Success);
        var dup = await _db.Tags.CreateAsync("vip", "#C42B1C");
        Assert.False(dup.Success);
        Assert.Equal(TagRepository.Duplicate, dup.ErrorCode);
        Assert.Equal("#6B4FBB", (await _db.Tags.ListAsync()).Single().ColorHex);   // stored upper-case
    }

    [Theory]
    [InlineData("", "#0F6CBD", TagRepository.Required)]
    [InlineData("X", "red", TagRepository.Invalid)]
    [InlineData("X", "#12345", TagRepository.Invalid)]
    public async Task Create_validates(string name, string color, string code)
    {
        var r = await _db.Tags.CreateAsync(name, color);
        Assert.False(r.Success);
        Assert.Equal(code, r.ErrorCode);
    }

    [Fact]
    public async Task Rename_and_recolour_keep_contact_links()
    {
        var tag = (await _db.Tags.CreateAsync("Brdal", "#0F6CBD")).Tag!;
        await Contact("9876543210", tag.Id);
        Assert.True((await _db.Tags.UpdateAsync(tag.Id, "Bridal", "#B4009E")).Success);
        var info = Assert.Single(await _db.Tags.ListAsync());
        Assert.Equal(("Bridal", "#B4009E", 1), (info.Name, info.ColorHex, info.ContactCount));
    }

    [Fact]
    public async Task Rename_to_existing_name_fails()
    {
        await _db.Tags.CreateAsync("A", "#0F6CBD");
        var b = (await _db.Tags.CreateAsync("B", "#0F6CBD")).Tag!;
        Assert.Equal(TagRepository.Duplicate, (await _db.Tags.UpdateAsync(b.Id, "a", "#0F6CBD")).ErrorCode);
    }

    [Fact]
    public async Task Merge_moves_contacts_without_duplicating_links_and_removes_source()
    {
        var src = (await _db.Tags.CreateAsync("Bridal", "#0F6CBD")).Tag!;
        var tgt = (await _db.Tags.CreateAsync("Bridal 2026", "#B4009E")).Tag!;
        await Contact("9876543210", src.Id);              // only source
        await Contact("9876543211", src.Id, tgt.Id);      // both
        await Contact("9876543212", tgt.Id);              // only target

        var r = await _db.Tags.MergeAsync(src.Id, tgt.Id);
        Assert.True(r.Success);

        var tags = await _db.Tags.ListAsync();
        var remaining = Assert.Single(tags);
        Assert.Equal("Bridal 2026", remaining.Name);
        Assert.Equal(3, remaining.ContactCount);
        Assert.Equal(3, (await _db.Contacts.SearchAsync(new ContactQuery(TagIds: [tgt.Id]))).TotalCount);
    }

    [Fact]
    public async Task Merge_into_itself_is_rejected()
    {
        var t = (await _db.Tags.CreateAsync("A", "#0F6CBD")).Tag!;
        Assert.Equal(TagRepository.SameTag, (await _db.Tags.MergeAsync(t.Id, t.Id)).ErrorCode);
    }

    [Fact]
    public async Task Delete_removes_links_but_not_contacts()
    {
        var t = (await _db.Tags.CreateAsync("Temp", "#0F6CBD")).Tag!;
        var c = await Contact("9876543210", t.Id);
        Assert.True((await _db.Tags.DeleteAsync(t.Id)).Success);
        Assert.Empty(await _db.Tags.ListAsync());
        Assert.Empty((await _db.Contacts.GetAsync(c.Id))!.ContactTags);
    }

    [Fact]
    public async Task Tag_counts_ignore_soft_deleted_contacts()
    {
        var t = (await _db.Tags.CreateAsync("VIP", "#0F6CBD")).Tag!;
        var a = await Contact("9876543210", t.Id);
        await Contact("9876543211", t.Id);
        await _db.Contacts.SoftDeleteAsync([a.Id]);
        Assert.Equal(1, (await _db.Tags.ListAsync()).Single().ContactCount);
    }
}
