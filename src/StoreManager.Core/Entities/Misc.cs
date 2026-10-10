namespace StoreManager.Core.Entities;

/// <summary>Key/value application setting. Append-only style table, no soft delete.</summary>
public class Setting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>Append-only audit trail. Never soft-deleted.</summary>
public class ActivityLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? UserId { get; set; }
    public DateTime At { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public Guid? EntityId { get; set; }
    public string? Details { get; set; }
}

public enum AttachmentKind { Photo = 0, Bill = 1, Other = 2 }

public class Attachment : EntityBase
{
    public string OwnerType { get; set; } = "";
    public Guid OwnerId { get; set; }
    public string FilePath { get; set; } = "";
    public AttachmentKind Kind { get; set; }
    public string? Caption { get; set; }
}

public static class SettingKeys
{
    public const string Theme = "Theme";                 // System | Light | Dark
    public const string Language = "Language";           // en | hi
    public const string LastPage = "LastPage";
    public const string WindowWidth = "WindowWidth";
    public const string WindowHeight = "WindowHeight";
    public const string StoreName = "StoreName";
    public const string DemoDataLoaded = "DemoDataLoaded";
}

public static class SystemTags
{
    public const string Demo = "Demo data";
}
