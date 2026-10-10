using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using StoreManager.App.Services;
using StoreManager.App.Views;
using StoreManager.Core.Entities;
using StoreManager.Data;
using StoreManager.ViewModels.Infrastructure;
using Windows.Graphics;
using VirtualKey = Windows.System.VirtualKey;
using VirtualKeyModifiers = Windows.System.VirtualKeyModifiers;

namespace StoreManager.App;

public sealed partial class MainWindow : Window
{
    // Order matches Ctrl+1 … Ctrl+8.
    private static readonly string[] PageOrder =
        { "dashboard", "tasks", "followups", "contacts", "payments", "fallpico", "reports", "settings" };

    private readonly SettingsStore _settings = App.Services.GetRequiredService<SettingsStore>();
    private readonly ILocalizer _loc = App.Services.GetRequiredService<ILocalizer>();
    private readonly DispatcherQueueTimer _toastTimer;
    private Func<Task>? _toastAction;

    public MainWindow()
    {
        InitializeComponent();
        Title = _loc.Get("App_Title");

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 1280;
            presenter.PreferredMinimumHeight = 720;
        }

        _toastTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); Toast.IsOpen = false; };
        App.Services.GetRequiredService<ToastService>().ToastRequested += m =>
            DispatcherQueue.TryEnqueue(() => ShowToast(m));

        RegisterShortcuts();
        Closed += OnClosed;
    }

    // ---------- navigation ----------

    public void NavigateTo(string tag)
    {
        var item = Nav.MenuItems.Concat(Nav.FooterMenuItems).OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == tag);
        if (item is null) return;
        if (Nav.SelectedItem == item) return;
        Nav.SelectedItem = item;          // triggers SelectionChanged
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag) return;
        Type page = tag switch
        {
            "dashboard" => typeof(DashboardPage),
            "contacts" => typeof(ContactsPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(ComingSoonPage)
        };
        ContentFrame.Navigate(page, tag);
        _ = _settings.SetAsync(SettingKeys.LastPage, tag);
    }

    public void OpenTagManager() => ContentFrame.Navigate(typeof(TagManagerPage));

    public void BackToContacts() => NavigateTo("contacts");

    // ---------- startup / shutdown ----------

    public async Task RestoreStateAsync()
    {
        var w = int.TryParse(await _settings.GetAsync(SettingKeys.WindowWidth, ""), out var ww) ? ww : 1400;
        var h = int.TryParse(await _settings.GetAsync(SettingKeys.WindowHeight, ""), out var hh) ? hh : 820;
        AppWindow.Resize(new SizeInt32(Math.Max(w, 1280), Math.Max(h, 720)));

        var theme = await _settings.GetAsync(SettingKeys.Theme, "System");
        App.Services.GetRequiredService<IPlatformActions>().ApplyTheme(theme);

        var last = await _settings.GetAsync(SettingKeys.LastPage, "dashboard");
        NavigateTo(PageOrder.Contains(last) ? last : "dashboard");
        if (Nav.SelectedItem is null) NavigateTo("dashboard");
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        try
        {
            var size = AppWindow.Size;
            _settings.SetAsync(SettingKeys.WindowWidth, size.Width.ToString()).GetAwaiter().GetResult();
            _settings.SetAsync(SettingKeys.WindowHeight, size.Height.ToString()).GetAwaiter().GetResult();
        }
        catch { /* never block shutdown */ }
    }

    // ---------- keyboard shortcuts ----------

    private void RegisterShortcuts()
    {
        for (var i = 0; i < PageOrder.Length; i++)
        {
            var tag = PageOrder[i];
            AddAccelerator(VirtualKey.Number1 + i, VirtualKeyModifiers.Control, () => NavigateTo(tag));
        }
        AddAccelerator(VirtualKey.N, VirtualKeyModifiers.Control, async () =>
        {
            if (ContentFrame.Content is INewItemPage p) await p.NewItemAsync();
        });
        AddAccelerator(VirtualKey.F5, VirtualKeyModifiers.None, async () =>
        {
            if (ContentFrame.Content is IRefreshablePage p) await p.RefreshAsync();
        });
    }

    private void AddAccelerator(VirtualKey key, VirtualKeyModifiers mods, Action action) =>
        AddAccelerator(key, mods, () => { action(); return Task.CompletedTask; });

    private void AddAccelerator(VirtualKey key, VirtualKeyModifiers mods, Func<Task> action)
    {
        var accel = new KeyboardAccelerator { Key = key, Modifiers = mods };
        accel.Invoked += async (_, e) => { e.Handled = true; await action(); };
        Root.KeyboardAccelerators.Add(accel);
    }

    // ---------- toast ----------

    private void ShowToast(ToastMessage m)
    {
        Toast.Message = m.Message;
        Toast.Severity = m.Kind switch
        {
            ToastKind.Warning => InfoBarSeverity.Warning,
            ToastKind.Error => InfoBarSeverity.Error,
            ToastKind.Info => InfoBarSeverity.Informational,
            _ => InfoBarSeverity.Success
        };
        _toastAction = m.Action;
        ToastAction.Content = m.ActionText;
        ToastAction.Visibility = m.ActionText is null ? Visibility.Collapsed : Visibility.Visible;
        Toast.IsOpen = true;

        _toastTimer.Stop();
        _toastTimer.Interval = TimeSpan.FromSeconds(m.ActionText is null ? 4 : 8);
        _toastTimer.Start();
    }

    private async void ToastAction_Click(object sender, RoutedEventArgs e)
    {
        var action = _toastAction;
        _toastAction = null;
        Toast.IsOpen = false;
        if (action is not null) await action();
    }
}
