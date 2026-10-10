using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using StoreManager.Core.Abstractions;

namespace StoreManager.Data;

/// <summary>Used only by `dotnet ef migrations add`.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new AppDbContext(options, new SystemClock(), new CurrentUser());
    }
}
