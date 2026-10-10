using CommunityToolkit.Mvvm.ComponentModel;
using StoreManager.Core.Abstractions;
using StoreManager.Core.Models;
using StoreManager.Data;
using StoreManager.Core.Entities;
using StoreManager.ViewModels.Infrastructure;

namespace StoreManager.ViewModels;

/// <summary>Step (a) placeholder: the full "Today" dashboard arrives in step (f).</summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IContactRepository _contacts;
    private readonly SettingsStore _settings;
    private readonly ILocalizer _loc;

    public DashboardViewModel(IContactRepository contacts, SettingsStore settings, ILocalizer loc)
    {
        _contacts = contacts; _settings = settings; _loc = loc;
    }

    [ObservableProperty] private string _greeting = "";
    [ObservableProperty] private int _contactCount;
    [ObservableProperty] private string _contactCountText = "";

    public async Task LoadAsync()
    {
        var store = await _settings.GetAsync(SettingKeys.StoreName, "");
        Greeting = string.IsNullOrWhiteSpace(store) ? _loc.Get("Dashboard_Welcome") : _loc.Format("Dashboard_WelcomeStore", store);
        var page = await _contacts.SearchAsync(new ContactQuery(Take: 1));
        ContactCount = page.TotalCount;
        ContactCountText = _loc.Format("Contacts_Count", page.TotalCount);
    }
}
