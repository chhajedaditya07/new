using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoreManager.Core.Abstractions;
using StoreManager.Data.Repositories;
using StoreManager.Data.Seed;

namespace StoreManager.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddStoreData(this IServiceCollection services, string databasePath)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<CurrentUser>();
        services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());

        services.AddDbContextFactory<AppDbContext>(o => o
            .UseSqlite($"Data Source={databasePath}")
            .AddInterceptors(new SqlitePragmaInterceptor()));

        services.AddSingleton<IContactRepository, ContactRepository>();
        services.AddSingleton<ITagRepository, TagRepository>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<SettingsStore>();
        services.AddSingleton<DemoDataService>();
        return services;
    }
}
