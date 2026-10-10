using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoreManager.Core.Entities;
using StoreManager.Data;
using StoreManager.Data.Seed;
using StoreManager.ViewModels.Infrastructure;

namespace StoreManager.ViewModels.Settings;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _settings;
    private readonly DemoDataService _demo;
    private readonly IPlatformActions _platform;
    private readonly IToastService _toast;
    private readonly ILocalizer _loc;
    private bool _loading;

    public SettingsViewModel(SettingsStore settings, DemoDataService demo, IPlatformActions platform,
        IToastService toast, ILocalizer loc)
    {
        _settings = settings; _demo = demo; _platform = platform; _toast = toast; _loc = loc;
        ThemeOptions = new List<Option<string>>
        {
            new("System", loc.Get("Theme_System")), new("Light", loc.Get("Theme_Light")), new("Dark", loc.Get("Theme_Dark"))
        };
        LanguageOptions = new List<Option<string>> { new("en", "English"), new("hi", "हिन्दी") };
        _selectedTheme = ThemeOptions[0];
        _selectedLanguage = LanguageOptions[0];
    }

    public IReadOnlyList<Option<string>> ThemeOptions { get; }
    public IReadOnlyList<Option<string>> LanguageOptions { get; }

    [ObservableProperty] private Option<string> _selectedTheme;
    [ObservableProperty] private Option<string> _selectedLanguage;
    [ObservableProperty] private string _storeName = "";
    [ObservableProperty] private bool _demoLoaded;
    [ObservableProperty] private bool _restartNeeded;

    public async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var theme = await _settings.GetAsync(SettingKeys.Theme, "System");
            var lang = await _settings.GetAsync(SettingKeys.Language, "en");
            SelectedTheme = ThemeOptions.FirstOrDefault(o => o.Value == theme) ?? ThemeOptions[0];
            SelectedLanguage = LanguageOptions.FirstOrDefault(o => o.Value == lang) ?? LanguageOptions[0];
            StoreName = await _settings.GetAsync(SettingKeys.StoreName, "");
            DemoLoaded = await _demo.IsLoadedAsync();
        }
        finally { _loading = false; }
    }

    partial void OnSelectedThemeChanged(Option<string> value)
    {
        if (_loading || value is null) return;
        _ = _settings.SetAsync(SettingKeys.Theme, value.Value);
        _platform.ApplyTheme(value.Value);          // takes effect immediately
    }

    partial void OnSelectedLanguageChanged(Option<string> value)
    {
        if (_loading || value is null) return;
        _ = _settings.SetAsync(SettingKeys.Language, value.Value);
        RestartNeeded = true;                       // string resources are read at start-up
    }

    /// <summary>Saved when the box loses focus (View calls this).</summary>
    [RelayCommand]
    public async Task SaveStoreNameAsync() => await _settings.SetAsync(SettingKeys.StoreName, StoreName.Trim());

    [RelayCommand]
    public async Task LoadDemoAsync()
    {
        var n = await _demo.LoadAsync();
        DemoLoaded = true;
        _toast.Show(_loc.Format("Toast_DemoLoaded", n));
    }

    /// <summary>The view confirms with the user first.</summary>
    public async Task RemoveDemoAsync()
    {
        var n = await _demo.RemoveAsync();
        DemoLoaded = false;
        _toast.Show(_loc.Format("Toast_DemoRemoved", n), ToastKind.Info);
    }
}
