using ClosedXML.Excel;
using StoreManager.Core.Entities;
using StoreManager.Services.Contacts;
using StoreManager.ViewModels.Contacts;
using StoreManager.ViewModels.Infrastructure;

namespace StoreManager.Tests.ViewModels;

public class ContactsViewModelTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private FakeToast _toast = null!;
    private FakeFiles _files = null!;
    private ContactsViewModel _vm = null!;

    public async Task InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _toast = new FakeToast();
        _files = new FakeFiles();
        var loc = new ReswLocalizer();
        var panel = new ContactPanelViewModel(_db.Contacts, _db.Tags, _toast, loc, new FakePlatform()) { DuplicateCheckDelay = TimeSpan.Zero };
        _vm = new ContactsViewModel(_db.Contacts, _db.Tags, new ContactImportService(_db.Contacts), _db.Demo, _toast, loc, _files, panel)
            { SearchDebounce = TimeSpan.Zero };
    }
    public Task DisposeAsync() { _db.Dispose(); return Task.CompletedTask; }

    private async Task<Contact> Add(string name, string mobile, ContactType type = ContactType.Customer, params Guid[] tags) =>
        (await _db.Contacts.SaveAsync(new Contact { Name = name, Mobile = mobile, Type = type }, tags)).Contact!;

    private static string[] Names(ContactsViewModel vm) => vm.Items.Select(i => i.Name).ToArray();

    [Fact]
    public async Task Empty_database_shows_the_empty_state_not_a_blank_screen()
    {
        Assert.False(_vm.ShowEmptyState);                 // before first load nothing is claimed
        await _vm.InitializeAsync();
        Assert.True(_vm.ShowEmptyState);
        Assert.False(_vm.ShowNoMatches);
        Assert.False(_vm.ShowSkeleton);
        Assert.Equal("0 contacts", _vm.CountText);
    }

    [Fact]
    public async Task Loads_first_page_with_display_ready_rows()
    {
        var vip = (await _db.Tags.CreateAsync("VIP", "#6B4FBB")).Tag!;
        await Add("Aarti Sharma", "9876543210", ContactType.Customer, vip.Id);
        await _vm.InitializeAsync();

        var row = Assert.Single(_vm.Items);
        Assert.Equal("+91 98765 43210", row.MobileDisplay);
        Assert.Equal("Customer", row.TypeLabel);
        Assert.Equal("VIP", Assert.Single(row.Tags).Name);
        Assert.Equal("1 contacts", _vm.CountText);
        Assert.False(_vm.ShowEmptyState);
    }

    [Fact]
    public async Task Scrolling_appends_pages_until_everything_is_loaded()
    {
        for (var i = 0; i < 120; i++) await Add($"P {i:000}", $"98{i:00000000}");
        await _vm.InitializeAsync();
        Assert.Equal(50, _vm.Items.Count);
        Assert.True(_vm.HasMore);

        Assert.Equal(50, await _vm.LoadMoreAsync());
        Assert.Equal(20, await _vm.LoadMoreAsync());
        Assert.Equal(120, _vm.Items.Count);
        Assert.False(_vm.HasMore);
        Assert.Equal(0, await _vm.LoadMoreAsync());
        Assert.Equal(120, _vm.Items.Select(i => i.Id).Distinct().Count());   // no duplicates across pages
    }

    [Fact]
    public async Task Search_filters_and_the_last_keystroke_wins()
    {
        await Add("Aarti Sharma", "9876543210");
        await Add("Meena Gupta", "9123456780");
        await Add("Meera Jain", "9000011111");
        await _vm.InitializeAsync();

        _vm.SearchText = "a";
        _vm.SearchText = "meer";
        await _vm.WhenIdleAsync();
        Assert.Equal(["Meera Jain"], Names(_vm));
        Assert.True(_vm.HasActiveFilters);
    }

    [Fact]
    public async Task No_matches_state_appears_for_filters_and_clearing_restores_the_list()
    {
        await Add("Aarti", "9876543210");
        await _vm.InitializeAsync();
        _vm.SearchText = "zzz";
        await _vm.WhenIdleAsync();
        Assert.True(_vm.ShowNoMatches);
        Assert.False(_vm.ShowEmptyState);

        _vm.ClearFiltersCommand.Execute(null);
        await _vm.WhenIdleAsync();
        Assert.Single(_vm.Items);
        Assert.False(_vm.ShowNoMatches);
        Assert.False(_vm.HasActiveFilters);
    }

    [Fact]
    public async Task Type_chips_filter_the_list()
    {
        await Add("Cust", "9876543210");
        await Add("Supp", "9876543211", ContactType.Supplier);
        await _vm.InitializeAsync();
        _vm.TypeFilters.Single(t => t.Type == ContactType.Supplier).IsSelected = true;
        await _vm.WhenIdleAsync();
        Assert.Equal(["Supp"], Names(_vm));
    }

    [Fact]
    public async Task Tag_filter_supports_any_and_all()
    {
        var vip = (await _db.Tags.CreateAsync("VIP", "#6B4FBB")).Tag!;
        var bridal = (await _db.Tags.CreateAsync("Bridal 2026", "#B4009E")).Tag!;
        await Add("OnlyVip", "9876543210", ContactType.Customer, vip.Id);
        await Add("Both", "9876543211", ContactType.Customer, vip.Id, bridal.Id);
        await _vm.InitializeAsync();

        _vm.TagFilters.Single(t => t.Id == vip.Id).IsSelected = true;
        _vm.TagFilters.Single(t => t.Id == bridal.Id).IsSelected = true;
        await _vm.WhenIdleAsync();
        Assert.Equal(["Both", "OnlyVip"], Names(_vm));
        Assert.Equal("Tags (2)", _vm.TagFilterSummary);

        _vm.TagMatchAll = true;
        await _vm.WhenIdleAsync();
        Assert.Equal(["Both"], Names(_vm));
    }

    [Fact]
    public async Task Sorting_changes_the_order()
    {
        await Add("Alpha", "9876543210");
        await Add("Bravo", "9876543211");
        await _vm.InitializeAsync();
        _vm.SelectedSort = _vm.SortOptions.Single(o => o.Label == "Name Z–A");
        await _vm.WhenIdleAsync();
        Assert.Equal(["Bravo", "Alpha"], Names(_vm));
    }

    [Fact]
    public async Task Delete_removes_the_row_and_the_Undo_toast_brings_it_back()
    {
        var a = await Add("Aarti", "9876543210");
        await Add("Meena", "9123456780");
        await _vm.InitializeAsync();

        await _vm.DeleteAsync([a.Id]);
        Assert.Equal(["Meena"], Names(_vm));
        Assert.Equal("1 contact(s) deleted", _toast.Last!.Message);
        Assert.Equal("Undo", _toast.Last.ActionText);

        await _toast.Last.Action!();
        Assert.Equal(["Aarti", "Meena"], Names(_vm));
    }

    [Fact]
    public async Task Deleting_the_open_contact_closes_the_panel()
    {
        var a = await Add("Aarti", "9876543210");
        await _vm.InitializeAsync();
        await _vm.SetSelectionAsync([a.Id]);
        Assert.True(_vm.Panel.IsViewing);

        await _vm.DeleteSelectedCommand.ExecuteAsync(null);
        Assert.False(_vm.Panel.IsOpen);
    }

    [Fact]
    public async Task Undo_warns_if_the_number_was_reused_in_the_meantime()
    {
        var a = await Add("Old", "9876543210");
        await _vm.InitializeAsync();
        await _vm.DeleteAsync([a.Id]);
        var undo = _toast.Last!.Action!;
        await Add("New", "9876543210");

        await undo();
        Assert.Equal(["New"], Names(_vm));
        Assert.Equal(ToastKind.Warning, _toast.Last!.Kind);
    }

    [Fact]
    public async Task Selecting_one_row_opens_its_profile_and_several_show_bulk_actions()
    {
        var a = await Add("Aarti", "9876543210");
        var b = await Add("Meena", "9123456780");
        await _vm.InitializeAsync();

        await _vm.SetSelectionAsync([a.Id]);
        Assert.True(_vm.Panel.IsViewing);
        Assert.Equal("Aarti", _vm.Panel.Title);
        Assert.False(_vm.HasMultiSelection);

        await _vm.SetSelectionAsync([a.Id, b.Id]);
        Assert.True(_vm.HasMultiSelection);
        Assert.Equal("2 selected", _vm.SelectionText);
    }

    [Fact]
    public async Task Bulk_tag_and_untag_update_the_rows()
    {
        var vip = (await _db.Tags.CreateAsync("VIP", "#6B4FBB")).Tag!;
        var a = await Add("Aarti", "9876543210");
        var b = await Add("Meena", "9123456780");
        await _vm.InitializeAsync();
        await _vm.SetSelectionAsync([a.Id, b.Id]);

        await _vm.BulkAddTagAsync(vip.Id);
        Assert.All(_vm.Items, r => Assert.Equal("VIP", Assert.Single(r.Tags).Name));

        await _vm.BulkRemoveTagAsync(vip.Id);
        Assert.All(_vm.Items, r => Assert.Empty(r.Tags));
    }

    [Fact]
    public async Task Saving_a_new_contact_from_the_panel_refreshes_the_list()
    {
        await _vm.InitializeAsync();
        await _vm.NewContactAsync();
        _vm.Panel.Name = "Fresh Face";
        _vm.Panel.Mobile = "9876543210";
        await _vm.Panel.SaveAsync();
        await Task.Delay(100);                           // Saved handler reloads asynchronously
        Assert.Equal(["Fresh Face"], Names(_vm));
    }

    [Fact]
    public async Task Import_previews_first_then_writes_on_confirm()
    {
        var csv = "Name,Mobile,Tags\nAarti,9876543210,VIP\nBroken,12,\nMeena,9123456780,\n";
        _files.NextOpen = new PickedFile("people.csv", new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv)));
        await _vm.InitializeAsync();

        Assert.True(await _vm.PrepareImportAsync());
        Assert.Equal("2 contacts are ready to import. 1 rows have problems and will be skipped.", _vm.ImportSummary);
        Assert.Empty(_vm.Items);                          // nothing written yet

        var outcome = await _vm.ConfirmImportAsync();
        Assert.Equal(2, outcome!.Created);
        Assert.Equal(["Aarti", "Meena"], Names(_vm));
        Assert.Contains(_vm.TagFilters, t => t.Name == "VIP");   // tag filter list refreshed after import
    }

    [Fact]
    public async Task Import_of_an_unsupported_file_type_is_rejected_with_a_message()
    {
        _files.NextOpen = new PickedFile("notes.pdf", new MemoryStream([1, 2, 3]));
        Assert.False(await _vm.PrepareImportAsync());
        Assert.Equal(ToastKind.Error, _toast.Last!.Kind);
    }

    [Fact]
    public async Task Import_cancelled_in_the_picker_does_nothing()
    {
        _files.NextOpen = null;
        Assert.False(await _vm.PrepareImportAsync());
        Assert.Empty(_toast.Shown);
    }

    [Fact]
    public async Task Corrupt_excel_file_gives_a_friendly_error_not_a_crash()
    {
        _files.NextOpen = new PickedFile("broken.xlsx", new MemoryStream(System.Text.Encoding.UTF8.GetBytes("this is not a workbook")));
        Assert.False(await _vm.PrepareImportAsync());
        Assert.Equal(ToastKind.Error, _toast.Last!.Kind);
    }

    [Fact]
    public async Task Export_writes_the_currently_filtered_contacts_to_excel()
    {
        await Add("Aarti", "9876543210");
        await Add("Meena", "9123456780");
        await _vm.InitializeAsync();
        _vm.SearchText = "meena";
        await _vm.WhenIdleAsync();

        await _vm.ExportCommand.ExecuteAsync(null);

        _files.SavedTo!.Position = 0;
        using var wb = new XLWorkbook(_files.SavedTo);
        var ws = wb.Worksheet(1);
        Assert.Equal("Name", ws.Cell(1, 1).GetString());
        Assert.Equal("Meena", ws.Cell(2, 1).GetString());
        Assert.True(ws.Cell(3, 1).IsEmpty());            // Aarti was filtered out
        Assert.Equal("1 contacts exported", _toast.Last!.Message);
    }

    [Fact]
    public async Task Export_cancelled_does_nothing()
    {
        await Add("Aarti", "9876543210");
        _files.CancelSave = true;
        await _vm.ExportCommand.ExecuteAsync(null);
        Assert.Empty(_toast.Shown);
    }

    [Fact]
    public async Task Demo_data_can_be_loaded_from_the_empty_state()
    {
        await _vm.InitializeAsync();
        Assert.True(_vm.ShowEmptyState);
        await _vm.LoadDemoCommand.ExecuteAsync(null);
        Assert.NotEmpty(_vm.Items);
        Assert.False(_vm.ShowEmptyState);
        Assert.Equal(60, _vm.TotalCount);
    }
}
