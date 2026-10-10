using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using StoreManager.ViewModels.Infrastructure;
using StoreManager.ViewModels.Tags;
using Windows.System;

namespace StoreManager.App.Views;

public sealed partial class TagManagerPage : Page
{
    private readonly ILocalizer _loc = App.Services.GetRequiredService<ILocalizer>();
    public TagManagerViewModel ViewModel { get; } = App.Services.GetRequiredService<TagManagerViewModel>();

    public TagManagerPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e) => await ViewModel.LoadAsync();

    private void Back_Click(object sender, RoutedEventArgs e) => App.MainWindow.BackToContacts();

    private async void NewName_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;
        await ViewModel.CreateAsync();
    }

    private static TagRowViewModel? RowOf(object sender) => (sender as FrameworkElement)?.Tag as TagRowViewModel;

    private void RowColor_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (RowOf(sender) is { } row && e.ClickedItem is Swatch swatch) row.ColorHex = swatch.Hex;
    }

    private void NewColorGrid_Loaded(object sender, RoutedEventArgs e) =>
        NewColorGrid.SelectedItem = TagManagerViewModel.Swatches.FirstOrDefault(s => s.Hex == ViewModel.NewColor);

    private void NewColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NewColorGrid.SelectedItem is Swatch swatch) ViewModel.NewColor = swatch.Hex;
    }

    private async void RowSave_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) await ViewModel.SaveRowAsync(row);
    }

    private async void RowMerge_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } source) return;
        var targets = ViewModel.Rows.Where(r => r.Id != source.Id && !r.IsSystem).ToList();
        if (targets.Count == 0) return;

        var combo = new ComboBox { ItemsSource = targets, DisplayMemberPath = nameof(TagRowViewModel.Name), MinWidth = 280, SelectedIndex = 0 };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = _loc.Format("Dialog_MergeBody", source.Name), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(combo);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _loc.Get("Dialog_MergeTitle"),
            Content = panel,
            PrimaryButtonText = _loc.Get("Dialog_Merge"),
            CloseButtonText = _loc.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && combo.SelectedItem is TagRowViewModel target)
            await ViewModel.MergeAsync(source.Id, target.Id);
    }

    private async void RowDelete_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _loc.Format("Dialog_DeleteTagTitle", row.Name),
            Content = _loc.Format("Dialog_DeleteTagBody", row.ContactCount),
            PrimaryButtonText = _loc.Get("Dialog_Delete"),
            CloseButtonText = _loc.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.DeleteAsync(row.Id);
    }
}
