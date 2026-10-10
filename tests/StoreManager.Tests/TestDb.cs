using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using StoreManager.Core.Abstractions;
using StoreManager.Data;
using StoreManager.Data.Seed;

namespace StoreManager.Tests;

/// <summary>A throw-away real SQLite database wired exactly like the app (migrations, WAL, filtered indexes).</summary>
public sealed class TestDb : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"sm-test-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _sp;

    public IContactRepository Contacts { get; }
    public ITagRepository Tags { get; }
    public SettingsStore Settings { get; }
    public DemoDataService Demo { get; }
    public Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext> Factory { get; }

    private TestDb()
    {
        _sp = new ServiceCollection().AddStoreData(_path).BuildServiceProvider();
        Contacts = _sp.GetRequiredService<IContactRepository>();
        Tags = _sp.GetRequiredService<ITagRepository>();
        Settings = _sp.GetRequiredService<SettingsStore>();
        Demo = _sp.GetRequiredService<DemoDataService>();
        Factory = _sp.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext>>();
    }

    public static async Task<TestDb> CreateAsync()
    {
        var db = new TestDb();
        await db._sp.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        return db;
    }

    public void Dispose()
    {
        _sp.Dispose();
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _path, _path + "-wal", _path + "-shm" })
            try { File.Delete(f); } catch { /* best effort */ }
    }
}
