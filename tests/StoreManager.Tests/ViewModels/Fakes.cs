using System.Xml.Linq;
using StoreManager.ViewModels.Infrastructure;

namespace StoreManager.Tests.ViewModels;

public static class RepoRoot
{
    public static string Path
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(System.IO.Path.Combine(dir.FullName, "StoreManager.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("StoreManager.sln not found above " + AppContext.BaseDirectory);
        }
    }
}

/// <summary>Reads the real Resources.resw, so a missing string fails a test instead of showing a raw key in the app.</summary>
public sealed class ReswLocalizer : ILocalizer
{
    private readonly Dictionary<string, string> _map;

    public ReswLocalizer(string folder = "en-US") => _map = Load(folder);

    public static Dictionary<string, string> Load(string folder)
    {
        var path = System.IO.Path.Combine(RepoRoot.Path, "src", "StoreManager.App", "Strings", folder, "Resources.resw");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);
    }

    public string Get(string key) => _map.TryGetValue(key, out var v) ? v : $"[[missing:{key}]]";
}

public sealed class FakeToast : IToastService
{
    public record Entry(string Message, ToastKind Kind, string? ActionText, Func<Task>? Action);
    public List<Entry> Shown { get; } = new();
    public Entry? Last => Shown.LastOrDefault();

    public void Show(string message, ToastKind kind = ToastKind.Success, string? actionText = null, Func<Task>? action = null) =>
        Shown.Add(new Entry(message, kind, actionText, action));
}

public sealed class FakeFiles : IFileDialogs
{
    public PickedFile? NextOpen { get; set; }
    public MemoryStream? SavedTo { get; private set; }
    public bool CancelSave { get; set; }

    public Task<PickedFile?> PickOpenAsync(IReadOnlyList<string> extensions, CancellationToken ct = default) =>
        Task.FromResult(NextOpen);

    public Task<Stream?> PickSaveAsync(string suggestedName, string extension, CancellationToken ct = default)
    {
        if (CancelSave) return Task.FromResult<Stream?>(null);
        SavedTo = new NonClosingMemoryStream();
        return Task.FromResult<Stream?>(SavedTo);
    }

    /// <summary>The view model disposes the stream it is given; keep the bytes readable for assertions.</summary>
    private sealed class NonClosingMemoryStream : MemoryStream
    {
        protected override void Dispose(bool disposing) { }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public sealed class FakePlatform : IPlatformActions
{
    public List<string> Copied { get; } = new();
    public List<Uri> Opened { get; } = new();
    public bool DesktopWhatsAppInstalled { get; set; } = true;
    public string? Theme { get; private set; }

    public Task CopyTextAsync(string text) { Copied.Add(text); return Task.CompletedTask; }

    public Task<bool> OpenUriAsync(Uri uri)
    {
        Opened.Add(uri);
        return Task.FromResult(uri.Scheme != "whatsapp" || DesktopWhatsAppInstalled);
    }

    public void ApplyTheme(string theme) => Theme = theme;
}
