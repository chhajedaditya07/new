using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using StoreManager.App.Services;
using StoreManager.ViewModels;
using StoreManager.ViewModels.Contacts;

namespace StoreManager.App.Views;

public sealed partial class DashboardPage : Page, INewItemPage, IRefreshablePage
{
    public DashboardViewModel ViewModel { get; } = App.Services.GetRequiredService<DashboardViewModel>();

    public DashboardPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e) => await ViewModel.LoadAsync();

    public Task RefreshAsync() => ViewModel.LoadAsync();

    public Task NewItemAsync() => StartNewContactAsync();

    private async void QuickContact_Click(object sender, RoutedEventArgs e) => await StartNewContactAsync();

    private void OpenContacts_Click(object sender, RoutedEventArgs e) => App.MainWindow.NavigateTo("contacts");

    private static async Task StartNewContactAsync()
    {
        App.MainWindow.NavigateTo("contacts");
        await App.Services.GetRequiredService<ContactsViewModel>().NewContactAsync();
    }
}
