using System.Diagnostics;
using StoreManager.Core.Entities;
using StoreManager.Core.Models;

namespace StoreManager.Tests.Data;

/// <summary>Backs the "smooth with 50,000+ contacts" requirement with a measurement.</summary>
public class PerformanceTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public async Task Fifty_thousand_contacts_stay_fast_to_page_search_and_filter()
    {
        using var db = await TestDb.CreateAsync();
        var vip = (await db.Tags.CreateAsync("VIP", "#6B4FBB")).Tag!;

        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            ctx.ChangeTracker.AutoDetectChangesEnabled = false;
            for (var i = 0; i < 50_000; i++)
            {
                var c = new Contact
                {
                    Name = $"Customer {i:00000} {(i % 7 == 0 ? "Sharma" : "Jain")}",
                    Mobile = "+91" + (6_000_000_000L + i).ToString(),
                    City = i % 3 == 0 ? "Surat" : "Jaipur",
                };
                if (i % 10 == 0) c.ContactTags.Add(new ContactTag { ContactId = c.Id, TagId = vip.Id });
                ctx.Contacts.Add(c);
                if (i % 5000 == 4999) await ctx.SaveChangesAsync();
            }
            await ctx.SaveChangesAsync();
        }

        // Warm up (JIT, connection, query cache), then measure.
        await db.Contacts.SearchAsync(new ContactQuery(Take: 50));

        var sw = Stopwatch.StartNew();
        var page1 = await db.Contacts.SearchAsync(new ContactQuery(Take: 50));
        var page1Ms = sw.ElapsedMilliseconds;

        sw.Restart();
        var byName = await db.Contacts.SearchAsync(new ContactQuery("customer 04999"));
        var nameMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var byPhone = await db.Contacts.SearchAsync(new ContactQuery("6000049999"));
        var phoneMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var byTag = await db.Contacts.SearchAsync(new ContactQuery(TagIds: [vip.Id], Skip: 2000, Take: 50));
        var tagMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var lookup = await db.Contacts.FindByMobileAsync("6000025000");
        var lookupMs = sw.ElapsedMilliseconds;

        output.WriteLine($"50k contacts: first page {page1Ms} ms, name search {nameMs} ms, phone search {phoneMs} ms, tag filter {tagMs} ms, mobile lookup {lookupMs} ms");
        Assert.Equal(50_000, page1.TotalCount);
        Assert.Single(byName.Items);
        Assert.Single(byPhone.Items);
        Assert.Equal(5_000, byTag.TotalCount);
        Assert.NotNull(lookup);

        // Generous ceilings (CI machines are slow); real numbers are typically well under 100 ms.
        Assert.True(page1Ms < 1500, $"first page took {page1Ms} ms");
        Assert.True(nameMs < 1500, $"name search took {nameMs} ms");
        Assert.True(phoneMs < 1500, $"phone search took {phoneMs} ms");
        Assert.True(tagMs < 1500, $"tag filter took {tagMs} ms");
        Assert.True(lookupMs < 200, $"mobile lookup took {lookupMs} ms");
    }
}
