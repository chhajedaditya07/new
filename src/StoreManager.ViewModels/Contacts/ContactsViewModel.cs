using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoreManager.Core.Abstractions;
using StoreManager.Core.Models;
using StoreManager.Data.Seed;
using StoreManager.Services.Contacts;
using StoreManager.ViewModels.Infrastructure;

namespace StoreManager.ViewModels.Contacts;

public sealed partial class ContactsViewModel : ObservableObject
{
    public const int PageSize = 50;

    private readonly IContactRepository _contacts;
    private readonly ITagRepository _tags;
    private readonly ContactImportService _importer;
    private readonly DemoDataService _demo;
    private readonly IToastService _toast;
    private readonly ILocalizer _loc;
    private readonly IFileDialogs _files;

    private CancellationTokenSource? _reloadCts;
    private int _loadVersion;
    private bool _suspendReload;
    private IReadOnlyList<Guid> _selectedIds = Array.Empty<Guid>();

    public ContactsViewModel(IContactRepository contacts, ITagRepository tags, ContactImportService importer,
        DemoDataService demo, IToastService toast, ILocalizer loc, IFileDialogs files, ContactPanelViewModel panel)
    {
        _contacts = contacts; _tags = tags; _importer = importer; _demo = demo;
        _toast = toast; _loc = loc; _files = files; Panel = panel;

        TypeFilters = Enum.GetValues<Core.Entities.ContactType>()
            .Select(t => new TypeFilterOption(t, loc.Get("ContactType_" + t))).ToList();
        foreach (var f in TypeFilters) f.PropertyChanged += (_, _) => OnFilterChanged();

        SortOptions = new List<Option<ContactSort>>
        {
            new(ContactSort.NameAsc, loc.Get("Sort_NameAsc")),
            new(ContactSort.NameDesc, loc.Get("Sort_NameDesc")),
            new(ContactSort.NewestFirst, loc.Get("Sort_Newest")),
            new(ContactSort.CityAsc, loc.Get("Sort_City")),
        };
        _selectedSort = SortOptions[0];

        panel.Saved += async (_, id) => { await ReloadAsync(); await RefreshTagFiltersAsync(); };
        panel.DeleteRequested += async (_, id) => await DeleteAsync(new[] { id });
        panel.OpenContactRequested += async (_, id) => await panel.ShowAsync(id);
    }

    public ContactPanelViewModel Panel { get; }

    /// <summary>Wait after the last keystroke before searching. Tests set this to zero.</summary>
    public TimeSpan SearchDebounce { get; set; } = TimeSpan.FromMilliseconds(250);

    public ObservableCollection<ContactRowViewModel> Items { get; } = new();
    public IReadOnlyList<TypeFilterOption> TypeFilters { get; }
    public IReadOnlyList<Option<ContactSort>> SortOptions { get; }
    public ObservableCollection<TagFilterOption> TagFilters { get; } = new();

    // ---------- filter state ----------
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _tagMatchAll;
    [ObservableProperty] private Option<ContactSort> _selectedSort;

    private Task _lastReload = Task.CompletedTask;

    /// <summary>Completes when the most recent filter-triggered reload has finished (used by tests).</summary>
    public Task WhenIdleAsync() => _lastReload;

    partial void OnSearchTextChanged(string value) { OnPropertyChanged(nameof(HasActiveFilters)); _lastReload = ReloadAsync(SearchDebounce); }
    partial void OnTagMatchAllChanged(bool value) { if (SelectedTagIds.Count > 1) OnFilterChanged(); }
    partial void OnSelectedSortChanged(Option<ContactSort> value) => OnFilterChanged();

    private void OnFilterChanged()
    {
        if (_suspendReload) return;
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(TagFilterSummary));
        _lastReload = ReloadAsync();
    }

    private List<Guid> SelectedTagIds => TagFilters.Where(t => t.IsSelected).Select(t => t.Id).ToList();

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchText) || TypeFilters.Any(t => t.IsSelected) || TagFilters.Any(t => t.IsSelected);

    public string TagFilterSummary
    {
        get
        {
            var n = TagFilters.Count(t => t.IsSelected);
            return n == 0 ? _loc.Get("Filter_Tags") : _loc.Format("Filter_TagsSelected", n);
        }
    }

    // ---------- list state ----------
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CountText), nameof(HasMore))] private int _totalCount;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowSkeleton), nameof(ShowEmptyState), nameof(ShowNoMatches))] private bool _isLoading;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowEmptyState), nameof(ShowNoMatches))] private bool _hasLoadedOnce;
    [ObservableProperty] private bool _isImporting;

    public string CountText => _loc.Format("Contacts_Count", TotalCount);
    public bool HasMore => Items.Count < TotalCount;
    public bool ShowSkeleton => IsLoading && Items.Count == 0;
    public bool ShowEmptyState => HasLoadedOnce && !IsLoading && Items.Count == 0 && !HasActiveFilters;
    public bool ShowNoMatches => HasLoadedOnce && !IsLoading && Items.Count == 0 && HasActiveFilters;

    private ContactQuery BuildQuery(int skip, int take) => new(
        Text: SearchText,
        Types: TypeFilters.Where(t => t.IsSelected).Select(t => t.Type).ToList(),
        TagIds: SelectedTagIds,
        TagMatch: TagMatchAll ? TagMatchMode.All : TagMatchMode.Any,
        Sort: SelectedSort.Value,
        Skip: skip,
        Take: take);

    // ---------- loading ----------

    public async Task InitializeAsync()
    {
        await RefreshTagFiltersAsync();
        await ReloadAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await RefreshTagFiltersAsync();
        await ReloadAsync();
    }

    /// <summary>Reloads page 1. A newer reload cancels and supersedes an older one (fast typing never shows stale results).</summary>
    public async Task ReloadAsync(TimeSpan? delay = null)
    {
        _reloadCts?.Cancel();
        var cts = _reloadCts = new CancellationTokenSource();
        var version = ++_loadVersion;
        IsLoading = true;
        try
        {
            if (delay is { } d && d > TimeSpan.Zero) await Task.Delay(d, cts.Token);
            var page = await _contacts.SearchAsync(BuildQuery(0, PageSize), cts.Token);
            if (version != _loadVersion) return;

            Items.Clear();
            foreach (var it in page.Items) Items.Add(new ContactRowViewModel(it, _loc));
            TotalCount = page.TotalCount;
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasMore));
        }
        catch (OperationCanceledException) { /* superseded */ }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
                OnPropertyChanged(nameof(ShowSkeleton));
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(ShowNoMatches));
            }
        }
    }

    /// <summary>Appends the next page; called by the list as the user scrolls.</summary>
    public async Task<int> LoadMoreAsync()
    {
        if (IsLoading || !HasMore) return 0;
        var version = _loadVersion;
        var page = await _contacts.SearchAsync(BuildQuery(Items.Count, PageSize));
        if (version != _loadVersion) return 0;     // filters changed while loading
        foreach (var it in page.Items) Items.Add(new ContactRowViewModel(it, _loc));
        TotalCount = page.TotalCount;
        OnPropertyChanged(nameof(HasMore));
        return page.Items.Count;
    }

    public async Task RefreshTagFiltersAsync()
    {
        var selected = TagFilters.Where(t => t.IsSelected).Select(t => t.Id).ToHashSet();
        var tags = await _tags.ListAsync();
        _suspendReload = true;
        try
        {
            TagFilters.Clear();
            foreach (var t in tags.Where(t => !t.IsSystem || t.ContactCount > 0))
            {
                var opt = new TagFilterOption(t.Id, t.Name, t.ColorHex, t.ContactCount, t.IsSystem) { IsSelected = selected.Contains(t.Id) };
                opt.PropertyChanged += (_, _) => OnFilterChanged();
                TagFilters.Add(opt);
            }
        }
        finally { _suspendReload = false; }
        OnPropertyChanged(nameof(TagFilterSummary));
        OnPropertyChanged(nameof(HasActiveFilters));
    }

    [RelayCommand]
    public void ClearFilters()
    {
        _suspendReload = true;
        SearchText = "";
        foreach (var f in TypeFilters) f.IsSelected = false;
        foreach (var f in TagFilters) f.IsSelected = false;
        _suspendReload = false;
        OnFilterChanged();
    }

    // ---------- selection ----------

    public int SelectionCount => _selectedIds.Count;
    public bool HasMultiSelection => _selectedIds.Count > 1;
    public bool HasSelection => _selectedIds.Count > 0;

    /// <summary>Called by the list when the user's selection changes. One selected row opens its profile.</summary>
    public async Task SetSelectionAsync(IReadOnlyList<Guid> ids)
    {
        _selectedIds = ids;
        OnPropertyChanged(nameof(SelectionCount));
        OnPropertyChanged(nameof(HasMultiSelection));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionText));
        if (ids.Count == 1) await Panel.ShowAsync(ids[0]);
    }

    public string SelectionText => _loc.Format("Bulk_Selected", _selectedIds.Count);

    // ---------- actions ----------

    [RelayCommand]
    public async Task NewContactAsync() => await Panel.BeginNewAsync();

    /// <summary>Soft-deletes and offers Undo. No confirmation dialog: the Undo toast is the safety net.</summary>
    public async Task DeleteAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0) return;
        var copy = ids.ToList();
        await _contacts.SoftDeleteAsync(copy);
        if (Panel.IsOpen && copy.Contains(Panel.CurrentId)) Panel.Close();
        await RefreshAsync();
        _toast.Show(_loc.Format("Toast_Deleted", copy.Count), ToastKind.Info, _loc.Get("Toast_Undo"), async () =>
        {
            var restored = await _contacts.RestoreAsync(copy);
            await RefreshAsync();
            if (restored < copy.Count)
                _toast.Show(_loc.Format("Toast_RestoreSkipped", copy.Count - restored), ToastKind.Warning);
        });
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync() => await DeleteAsync(_selectedIds.ToList());

    public async Task BulkAddTagAsync(Guid tagId)
    {
        await _contacts.AddTagsAsync(_selectedIds.ToList(), new[] { tagId });
        _toast.Show(_loc.Format("Toast_Tagged", _selectedIds.Count));
        await RefreshAsync();
    }

    public async Task BulkRemoveTagAsync(Guid tagId)
    {
        await _contacts.RemoveTagsAsync(_selectedIds.ToList(), new[] { tagId });
        _toast.Show(_loc.Format("Toast_Untagged", _selectedIds.Count));
        await RefreshAsync();
    }

    // ---------- import / export ----------

    [ObservableProperty] private ImportPreview? _importPreview;
    [ObservableProperty] private string _importSummary = "";

    /// <summary>Step 1 of import: pick a file and show what would happen. Nothing is written yet.</summary>
    public async Task<bool> PrepareImportAsync()
    {
        var file = await _files.PickOpenAsync(new[] { ".xlsx", ".csv", ".vcf" });
        if (file is null) return false;
        await using var stream = file.Content;
        var format = ContactImportService.DetectFormat(file.Name);
        if (format is null)
        {
            _toast.Show(_loc.Get("Toast_ImportUnsupported"), ToastKind.Error);
            return false;
        }
        try
        {
            var preview = await Task.Run(() => _importer.Preview(stream, format.Value));
            ImportPreview = preview;
            ImportSummary = _loc.Format("Import_Summary", preview.ValidRows.Count, preview.Issues.Count);
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or FormatException)
        {
            _toast.Show(_loc.Get("Toast_ImportUnreadable"), ToastKind.Error);
            return false;
        }
    }

    /// <summary>Step 2: write the valid rows in one transaction.</summary>
    public async Task<ImportOutcome?> ConfirmImportAsync()
    {
        if (ImportPreview is null) return null;
        IsImporting = true;
        try
        {
            var outcome = await _importer.ImportAsync(ImportPreview);
            ImportPreview = null;
            _toast.Show(_loc.Format("Toast_Imported", outcome.Created));
            await RefreshAsync();
            return outcome;
        }
        finally { IsImporting = false; }
    }

    [RelayCommand]
    public async Task ExportAsync()
    {
        var rows = await _contacts.ListForExportAsync(BuildQuery(0, int.MaxValue));
        await using var stream = await _files.PickSaveAsync("Contacts", ".xlsx");
        if (stream is null) return;
        await Task.Run(() => ContactExportService.WriteExcel(rows, stream));
        _toast.Show(_loc.Format("Toast_Exported", rows.Count));
    }

    [RelayCommand]
    public async Task LoadDemoAsync()
    {
        var n = await _demo.LoadAsync();
        _toast.Show(_loc.Format("Toast_DemoLoaded", n));
        await RefreshAsync();
    }
}
