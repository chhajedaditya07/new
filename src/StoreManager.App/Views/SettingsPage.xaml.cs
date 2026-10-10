using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using StoreManager.ViewModels.Infrastructure;
using StoreManager.ViewModels.Settings;

namespace StoreManager.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; } = App.Services.GetRequiredService<SettingsViewModel>();

    public SettingsPage()
    {
        InitializeComponent();
        DataFolderText.Text = App.Paths.DataDir;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e) => await ViewModel.LoadAsync();

    private async void StoreName_LostFocus(object sender, RoutedEventArgs e) => await ViewModel.SaveStoreNameAsync();

    private async void RemoveDemo_Click(object sender, RoutedEventArgs e)
    {
        var loc = App.Services.GetRequiredService<ILocalizer>();
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = loc.Get("Dialog_RemoveDemoTitle"),
            Content = loc.Get("Dialog_RemoveDemoBody"),
            PrimaryButtonText = loc.Get("Dialog_Remove"),
            CloseButtonText = loc.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.RemoveDemoAsync();
    }
}
