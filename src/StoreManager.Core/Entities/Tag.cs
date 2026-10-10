namespace StoreManager.Core.Entities;

public class Tag : EntityBase
{
    public string Name { get; set; } = "";
    public string ColorHex { get; set; } = "#0F6CBD";

    /// <summary>System tags (e.g. the demo-data marker) cannot be renamed, merged or deleted by users.</summary>
    public bool IsSystem { get; set; }
}

public class ContactTag
{
    public Guid ContactId { get; set; }
    public Contact Contact { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
