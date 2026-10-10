using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Globalization;
using StoreManager.App.Services;
using StoreManager.Core.Entities;
using StoreManager.Data;
using StoreManager.Data.Seed;
using StoreManager.Services.Contacts;
using StoreManager.ViewModels;
using StoreManager.ViewModels.Contacts;
using StoreManager.ViewModels.Infrastructure;
using StoreManager.ViewModels.Settings;
using StoreManager.ViewModels.Tags;

namespace StoreManager.App;

public partial class App : Application
{
    private static Mutex? _singleInstance;

    public static IServiceProvider Services { get; private set; } = null!;
    public static MainWindow MainWindow { get; private set; } = null!;
    public static AppPaths Paths { get; } = new();

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            Log(e.Exception);
            e.Handled = true;      // keep the shop running; the details are in the log
            try { Services?.GetService<IToastService>()?.Show(e.Exception.Message, ToastKind.Error); } catch { /* ignore */ }
        };
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Two copies on one database would only confuse staff.
        _singleInstance = new Mutex(true, @"Local\StoreManager.SingleInstance", out var isFirst);
        if (!isFirst) { Environment.Exit(0); return; }

        try
        {
            Services = BuildServices();
            await Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();

            // The UI language must be set before any string is loaded.
            var lang = await Services.GetRequiredService<SettingsStore>().GetAsync(SettingKeys.Language, "en");
            ApplicationLanguages.PrimaryLanguageOverride = lang == "hi" ? "hi-IN" : "en-US";

            MainWindow = new MainWindow();
            MainWindow.Activate();
            await MainWindow.RestoreStateAsync();
        }
        catch (Exception ex)
        {
            Log(ex);
            ShowFatal(ex);
        }
    }

    private static IServiceProvider BuildServices()
    {
        var s = new ServiceCollection();
        s.AddSingleton(Paths);
        s.AddStoreData(Paths.DatabasePath);

        s.AddSingleton<ToastService>();
        s.AddSingleton<IToastService>(sp => sp.GetRequiredService<ToastService>());
        s.AddSingleton<ILocalizer, ResourceLocalizer>();
        s.AddSingleton<IFileDialogs, FileDialogs>();
        s.AddSingleton<IPlatformActions, PlatformActions>();
        s.AddSingleton<ContactImportService>();

        s.AddSingleton<ContactPanelViewModel>();
        s.AddSingleton<ContactsViewModel>();
        s.AddSingleton<TagManagerViewModel>();
        s.AddSingleton<SettingsViewModel>();
        s.AddSingleton<DashboardViewModel>();
        return s.BuildServiceProvider();
    }

    public static void Log(Exception ex)
    {
        try
        {
            var file = Path.Combine(Paths.LogDir, $"error-{DateTime.Now:yyyy-MM-dd}.log");
            File.AppendAllText(file, $"[{DateTime.Now:HH:mm:ss}] {ex}\n\n");
        }
        catch { /* logging must never throw */ }
    }

    private static void ShowFatal(Exception ex)
    {
        var window = new Window { Title = "Store Manager" };
        window.Content = new Microsoft.UI.Xaml.Controls.TextBlock
        {
            Margin = new Thickness(24),
            TextWrapping = TextWrapping.Wrap,
            Text = $"The app could not start.\n\n{ex.Message}\n\nDetails were saved in:\n{Paths.LogDir}"
        };
        window.Activate();
    }
}
