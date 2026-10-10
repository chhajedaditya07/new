using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using StoreManager.App.Services;
using StoreManager.ViewModels.Contacts;
using StoreManager.ViewModels.Infrastructure;

namespace StoreManager.App.Views;

public sealed partial class ContactsPage : Page, INewItemPage, IRefreshablePage
{
    private readonly ILocalizer _loc = App.Services.GetRequiredService<ILocalizer>();
    private ScrollViewer? _scroll;
    private bool _loadingMore;
    private bool _initialized;

    public ContactsViewModel ViewModel { get; } = App.Services.GetRequiredService<ContactsViewModel>();

    public ContactsPage()
    {
        InitializeComponent();
        // When the panel is closed, also clear the highlighted row.
        ViewModel.Panel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ContactPanelViewModel.IsOpen) && !ViewModel.Panel.IsOpen)
                List.SelectedItems.Clear();
        };
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!_initialized) { _initialized = true; await ViewModel.InitializeAsync(); }
        else await ViewModel.RefreshAsync();
    }

    public Task NewItemAsync() => ViewModel.NewContactAsync();
    public Task RefreshAsync() => ViewModel.RefreshAsync();

    // ---------- infinite scroll ----------

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_scroll is not null) return;
        _scroll = FindDescendant<ScrollViewer>(List);
        if (_scroll is not null) _scroll.ViewChanged += Scroll_ViewChanged;
    }

    private async void Scroll_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_scroll is null || _loadingMore) return;
        if (_scroll.ScrollableHeight - _scroll.VerticalOffset > 600) return;
        _loadingMore = true;
        try { await ViewModel.LoadMoreAsync(); }
        finally { _loadingMore = false; }
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (FindDescendant<T>(child) is { } deeper) return deeper;
        }
        return null;
    }

    // ---------- selection ----------

    private async void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var ids = List.SelectedItems.OfType<ContactRowViewModel>().Select(r => r.Id).ToList();
        await ViewModel.SetSelectionAsync(ids);
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e) => List.SelectedItems.Clear();

    // ---------- bulk tag menus ----------

    private void AddTagFlyout_Opening(object? sender, object e) => FillTagMenu(AddTagFlyout, id => ViewModel.BulkAddTagAsync(id));

    private void RemoveTagFlyout_Opening(object? sender, object e) => FillTagMenu(RemoveTagFlyout, id => ViewModel.BulkRemoveTagAsync(id));

    private void FillTagMenu(MenuFlyout flyout, Func<Guid, Task> action)
    {
        flyout.Items.Clear();
        foreach (var tag in ViewModel.TagFilters.Where(t => !t.IsSystem))
        {
            var item = new MenuFlyoutItem { Text = tag.Name };
            var id = tag.Id;
            item.Click += async (_, _) => await action(id);
            flyout.Items.Add(item);
        }
        if (flyout.Items.Count == 0)
            flyout.Items.Add(new MenuFlyoutItem { Text = _loc.Get("Tags_Empty"), IsEnabled = false });
    }

    // ---------- navigation ----------

    private void ManageTags_Click(object sender, RoutedEventArgs e) => App.MainWindow.OpenTagManager();

    // ---------- form helpers ----------

    private void Phone_LostFocus(object sender, RoutedEventArgs e) => ViewModel.Panel.FormatPhoneFields();

    private async void SaveAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ViewModel.Panel.SaveAsync();
    }

    private async void CancelAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ViewModel.Panel.CancelAsync();
    }

    // ---------- import ----------

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (!await ViewModel.PrepareImportAsync() || ViewModel.ImportPreview is not { } preview) return;

        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(new TextBlock { Text = ViewModel.ImportSummary, TextWrapping = TextWrapping.Wrap });
        AddIssueList(body, preview.Issues.Select(i => (i.RowNumber, i.Message)));

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _loc.Get("Import_Title"),
            Content = new ScrollViewer { Content = body, MaxHeight = 420 },
            PrimaryButtonText = _loc.Format("Import_Button", preview.ValidRows.Count),
            IsPrimaryButtonEnabled = preview.ValidRows.Count > 0,
            CloseButtonText = _loc.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var outcome = await ViewModel.ConfirmImportAsync();
        if (outcome is { Issues.Count: > 0 })
        {
            var report = new StackPanel { Spacing = 10 };
            report.Children.Add(new TextBlock { Text = _loc.Format("Import_Result", outcome.Created, outcome.Issues.Count), TextWrapping = TextWrapping.Wrap });
            AddIssueList(report, outcome.Issues.Select(i => (i.RowNumber, i.Message)));
            await new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = _loc.Get("Import_ResultTitle"),
                Content = new ScrollViewer { Content = report, MaxHeight = 420 },
                CloseButtonText = _loc.Get("Dialog_Close")
            }.ShowAsync();
        }
    }

    private void AddIssueList(Panel parent, IEnumerable<(int Row, string Message)> issues)
    {
        var list = issues.Take(100).ToList();
        if (list.Count == 0) return;
        parent.Children.Add(new TextBlock { Text = _loc.Get("Import_Problems"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        foreach (var (row, message) in list)
            parent.Children.Add(new TextBlock { Text = $"{_loc.Format("Import_Row", row)}: {message}", TextWrapping = TextWrapping.Wrap, Opacity = 0.85 });
    }
}
