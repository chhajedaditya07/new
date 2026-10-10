namespace StoreManager.Core.Entities;

public class User : EntityBase
{
    public string Name { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Staff;
    public string? PinHash { get; set; }
    public string? PinSalt { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public string Language { get; set; } = "en";
    public bool IsActive { get; set; } = true;
    public string AvatarColor { get; set; } = "#0F6CBD";
}
