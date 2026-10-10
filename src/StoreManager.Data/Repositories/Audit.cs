using StoreManager.Core.Abstractions;
using StoreManager.Core.Entities;

namespace StoreManager.Data.Repositories;

internal static class Audit
{
    public static void Log(AppDbContext ctx, IClock clock, ICurrentUser user,
        string action, string entityType, Guid? entityId, string? details = null) =>
        ctx.ActivityLogs.Add(new ActivityLog
        {
            UserId = user.UserId,
            At = clock.UtcNow,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details
        });
}
