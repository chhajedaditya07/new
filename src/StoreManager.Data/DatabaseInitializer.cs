using Microsoft.EntityFrameworkCore;
using StoreManager.Core.Abstractions;
using StoreManager.Core.Entities;

namespace StoreManager.Data;

public sealed class DatabaseInitializer
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly CurrentUser _currentUser;

    public DatabaseInitializer(IDbContextFactory<AppDbContext> factory, CurrentUser currentUser)
    {
        _factory = factory;
        _currentUser = currentUser;
    }

    /// <summary>Applies migrations and makes sure an Owner exists. Until the PIN login ships (step g) the Owner is the active user.</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        await ctx.Database.MigrateAsync(ct);

        var owner = await ctx.Users.OrderBy(u => u.CreatedAt).FirstOrDefaultAsync(u => u.Role == UserRole.Owner, ct);
        if (owner is null)
        {
            owner = new User { Name = "Owner", Role = UserRole.Owner };
            ctx.Users.Add(owner);
            await ctx.SaveChangesAsync(ct);
        }
        _currentUser.UserId = owner.Id;
    }
}
