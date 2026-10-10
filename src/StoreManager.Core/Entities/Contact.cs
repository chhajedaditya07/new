namespace StoreManager.Core.Entities;

public class Contact : EntityBase
{
    public ContactType Type { get; set; } = ContactType.Customer;
    public string Name { get; set; } = "";

    /// <summary>Normalised, e.g. "+919876543210". Unique among non-deleted contacts.</summary>
    public string Mobile { get; set; } = "";
    public string? WhatsApp { get; set; }
    public string? AltPhone { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Gstin { get; set; }
    public DateOnly? Birthday { get; set; }
    public DateOnly? Anniversary { get; set; }
    public Guid? ReferredByContactId { get; set; }
    public string? Notes { get; set; }
    public Guid? PhotoAttachmentId { get; set; }

    public List<ContactTag> ContactTags { get; set; } = new();

    /// <summary>WhatsApp number, defaulting to the mobile.</summary>
    public string EffectiveWhatsApp => string.IsNullOrWhiteSpace(WhatsApp) ? Mobile : WhatsApp!;
}
