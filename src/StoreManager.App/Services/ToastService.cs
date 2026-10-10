using StoreManager.ViewModels.Infrastructure;

namespace StoreManager.App.Services;

public sealed record ToastMessage(string Message, ToastKind Kind, string? ActionText, Func<Task>? Action);

public sealed class ToastService : IToastService
{
    /// <summary>Raised on whichever thread called Show; the window marshals to the UI thread.</summary>
    public event Action<ToastMessage>? ToastRequested;

    public void Show(string message, ToastKind kind = ToastKind.Success, string? actionText = null, Func<Task>? action = null) =>
        ToastRequested?.Invoke(new ToastMessage(message, kind, actionText, action));
}
