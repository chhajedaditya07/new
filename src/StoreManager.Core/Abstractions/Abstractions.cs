namespace StoreManager.Core.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>Who is performing the current action. Set by the login flow (step g).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
}

public sealed class CurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
}
