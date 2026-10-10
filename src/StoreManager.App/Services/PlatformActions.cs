using Microsoft.UI.Xaml;
using StoreManager.ViewModels.Infrastructure;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace StoreManager.App.Services;

public sealed class PlatformActions : IPlatformActions
{
    public Task CopyTextAsync(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        Clipboard.Flush();
        return Task.CompletedTask;
    }

    public async Task<bool> OpenUriAsync(Uri uri)
    {
        try { return await Launcher.LaunchUriAsync(uri); }
        catch { return false; }
    }

    public void ApplyTheme(string theme)
    {
        if (App.MainWindow?.Content is FrameworkElement root)
            root.RequestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
    }
}
