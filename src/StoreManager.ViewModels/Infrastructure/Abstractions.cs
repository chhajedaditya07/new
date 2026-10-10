namespace StoreManager.ViewModels.Infrastructure;

/// <summary>Looks up UI text from the resource files (English / Hindi).</summary>
public interface ILocalizer
{
    string Get(string key);
}

public static class LocalizerExtensions
{
    public static string Format(this ILocalizer loc, string key, params object?[] args) =>
        string.Format(loc.Get(key), args);
}

public enum ToastKind { Success, Info, Warning, Error }

/// <summary>Non-blocking message at the bottom of the window, optionally with one action such as Undo.</summary>
public interface IToastService
{
    void Show(string message, ToastKind kind = ToastKind.Success, string? actionText = null, Func<Task>? action = null);
}

public sealed record PickedFile(string Name, Stream Content);

public interface IFileDialogs
{
    /// <summary>Returns null if the user cancelled.</summary>
    Task<PickedFile?> PickOpenAsync(IReadOnlyList<string> extensions, CancellationToken ct = default);

    /// <summary>Returns null if the user cancelled. The caller disposes the stream.</summary>
    Task<Stream?> PickSaveAsync(string suggestedName, string extension, CancellationToken ct = default);
}

/// <summary>Things only the OS shell can do.</summary>
public interface IPlatformActions
{
    Task CopyTextAsync(string text);
    Task<bool> OpenUriAsync(Uri uri);
    void ApplyTheme(string theme);   // System | Light | Dark
}

public sealed record Option<T>(T Value, string Label);
