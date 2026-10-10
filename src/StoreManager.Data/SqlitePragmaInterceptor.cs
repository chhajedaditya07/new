using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace StoreManager.Data;

/// <summary>WAL gives concurrent reads while the UI writes; NORMAL sync is safe with WAL and much faster.</summary>
public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private const string Sql = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000; PRAGMA foreign_keys=ON;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = Sql;
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = Sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
