using StoreManager.Core.Models;

namespace StoreManager.Tests.Data;

public class DemoDataTests
{
    [Fact]
    public async Task Load_then_remove_touches_only_demo_contacts()
    {
        using var db = await TestDb.CreateAsync();
        await db.Contacts.SaveAsync(new() { Name = "Real Customer", Mobile = "9000000001" }, []);

        Assert.False(await db.Demo.IsLoadedAsync());
        var created = await db.Demo.LoadAsync(40);
        Assert.Equal(40, created);
        Assert.True(await db.Demo.IsLoadedAsync());
        Assert.Equal(41, (await db.Contacts.SearchAsync(new ContactQuery())).TotalCount);

        var removed = await db.Demo.RemoveAsync();
        Assert.Equal(40, removed);
        var left = await db.Contacts.SearchAsync(new ContactQuery());
        Assert.Equal("Real Customer", Assert.Single(left.Items).Name);
        Assert.False(await db.Demo.IsLoadedAsync());
    }

    [Fact]
    public async Task Demo_data_is_deterministic_and_has_valid_unique_mobiles()
    {
        using var db = await TestDb.CreateAsync();
        await db.Demo.LoadAsync(60);
        var all = await db.Contacts.SearchAsync(new ContactQuery(Take: 200));
        Assert.Equal(60, all.TotalCount);
        Assert.Equal(60, all.Items.Select(i => i.Mobile).Distinct().Count());
        Assert.All(all.Items, i => Assert.Matches(@"^\+91[6-9]\d{9}$", i.Mobile));
    }
}
