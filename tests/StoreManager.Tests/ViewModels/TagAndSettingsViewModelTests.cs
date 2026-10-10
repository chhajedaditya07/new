using StoreManager.Core.Entities;
using StoreManager.ViewModels.Settings;
using StoreManager.ViewModels.Tags;

namespace StoreManager.Tests.ViewModels;

public class TagManagerViewModelTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private FakeToast _toast = null!;
    private TagManagerViewModel _vm = null!;

    public async Task InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _toast = new FakeToast();
        _vm = new TagManagerViewModel(_db.Tags, _toast, new ReswLocalizer());
    }
    public Task DisposeAsync() { _db.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task Empty_list_flags_the_empty_state()
    {
        await _vm.LoadAsync();
        Assert.True(_vm.IsEmpty);
    }

    [Fact]
    public async Task Create_adds_the_tag_clears_the_box_and_toasts()
    {
        _vm.NewName = "VIP";
        await _vm.CreateCommand.ExecuteAsync(null);
        Assert.Equal("VIP", Assert.Single(_vm.Rows).Name);
        Assert.Equal("", _vm.NewName);
        Assert.False(_vm.IsEmpty);
        Assert.Equal("Tag \"VIP\" added", _toast.Last!.Message);
    }

    [Fact]
    public async Task Create_errors_are_shown_inline_in_plain_language()
    {
        _vm.NewName = "  ";
        await _vm.CreateCommand.ExecuteAsync(null);
        Assert.Equal("Please enter a tag name.", _vm.Error);
        Assert.True(_vm.HasError);

        _vm.NewName = "VIP";
        await _vm.CreateCommand.ExecuteAsync(null);
        Assert.False(_vm.HasError);                       // success clears the old error

        _vm.NewName = "vip";
        await _vm.CreateCommand.ExecuteAsync(null);
        Assert.Equal("A tag with this name already exists.", _vm.Error);
    }

    [Fact]
    public async Task Editing_a_row_marks_it_dirty_and_saving_persists_it()
    {
        _vm.NewName = "Brdal";
        await _vm.CreateCommand.ExecuteAsync(null);
        var row = _vm.Rows.Single();
        Assert.False(row.IsDirty);

        row.Name = "Bridal";
        row.ColorHex = "#B4009E";
        Assert.True(row.IsDirty);

        Assert.True(await _vm.SaveRowAsync(row));
        var reloaded = _vm.Rows.Single();
        Assert.Equal(("Bridal", "#B4009E"), (reloaded.Name, reloaded.ColorHex));
        Assert.False(reloaded.IsDirty);
    }

    [Fact]
    public async Task A_rejected_rename_reverts_the_row_and_explains_why()
    {
        _vm.NewName = "A"; await _vm.CreateCommand.ExecuteAsync(null);
        _vm.NewName = "B"; await _vm.CreateCommand.ExecuteAsync(null);
        var b = _vm.Rows.Single(r => r.Name == "B");
        b.Name = "a";
        Assert.False(await _vm.SaveRowAsync(b));
        Assert.Equal("A tag with this name already exists.", _vm.Error);
        Assert.Equal(["A", "B"], _vm.Rows.Select(r => r.Name).ToArray());
    }

    [Fact]
    public async Task Merge_and_delete_work_and_report()
    {
        var src = (await _db.Tags.CreateAsync("Bridal", "#0F6CBD")).Tag!;
        var tgt = (await _db.Tags.CreateAsync("Bridal 2026", "#B4009E")).Tag!;
        await _db.Contacts.SaveAsync(new Contact { Name = "A", Mobile = "9876543210" }, [src.Id]);
        await _vm.LoadAsync();

        Assert.True(await _vm.MergeAsync(src.Id, tgt.Id));
        Assert.Equal("Bridal 2026", Assert.Single(_vm.Rows).Name);
        Assert.Equal(1, _vm.Rows[0].ContactCount);
        Assert.Equal("Merged into \"Bridal 2026\"", _toast.Last!.Message);

        Assert.True(await _vm.DeleteAsync(tgt.Id));
        Assert.True(_vm.IsEmpty);
    }

    [Fact]
    public async Task Merging_a_tag_into_itself_is_refused()
    {
        var t = (await _db.Tags.CreateAsync("A", "#0F6CBD")).Tag!;
        Assert.False(await _vm.MergeAsync(t.Id, t.Id));
        Assert.Equal("Pick a different tag to merge into.", _vm.Error);
    }

    [Fact]
    public async Task System_tags_are_shown_but_locked()
    {
        await _db.Demo.LoadAsync(3);
        await _vm.LoadAsync();
        var demo = _vm.Rows.Single(r => r.Name == SystemTags.Demo);
        Assert.False(demo.CanEdit);
        Assert.False(await _vm.DeleteAsync(demo.Id));
        Assert.Equal("This is a built-in tag and cannot be changed.", _vm.Error);
    }

    [Fact]
    public void Palette_has_distinct_valid_colours()
    {
        Assert.Equal(TagManagerViewModel.Palette.Count, TagManagerViewModel.Palette.Distinct().Count());
        Assert.All(TagManagerViewModel.Palette, c => Assert.Matches("^#[0-9A-F]{6}$", c));
        Assert.Equal(TagManagerViewModel.Palette.Count, TagManagerViewModel.Swatches.Count);
    }
}

public class SettingsViewModelTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private FakePlatform _platform = null!;
    private FakeToast _toast = null!;
    private SettingsViewModel _vm = null!;

    public async Task InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _platform = new FakePlatform();
        _toast = new FakeToast();
        _vm = new SettingsViewModel(_db.Settings, _db.Demo, _platform, _toast, new ReswLocalizer());
    }
    public Task DisposeAsync() { _db.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task Defaults_are_system_theme_and_english()
    {
        await _vm.LoadAsync();
        Assert.Equal("System", _vm.SelectedTheme.Value);
        Assert.Equal("en", _vm.SelectedLanguage.Value);
        Assert.False(_vm.RestartNeeded);
        Assert.False(_vm.DemoLoaded);
    }

    [Fact]
    public async Task Theme_change_applies_immediately_and_is_remembered()
    {
        await _vm.LoadAsync();
        _vm.SelectedTheme = _vm.ThemeOptions.Single(o => o.Value == "Dark");
        await Task.Delay(100);
        Assert.Equal("Dark", _platform.Theme);
        Assert.Equal("Dark", await _db.Settings.GetAsync(SettingKeys.Theme));
    }

    [Fact]
    public async Task Loading_saved_settings_does_not_trigger_side_effects()
    {
        await _db.Settings.SetAsync(SettingKeys.Theme, "Light");
        await _db.Settings.SetAsync(SettingKeys.Language, "hi");
        await _db.Settings.SetAsync(SettingKeys.StoreName, "Shree Sarees");
        await _vm.LoadAsync();
        Assert.Equal("Light", _vm.SelectedTheme.Value);
        Assert.Equal("hi", _vm.SelectedLanguage.Value);
        Assert.Equal("Shree Sarees", _vm.StoreName);
        Assert.Null(_platform.Theme);                    // not re-applied just because we loaded
        Assert.False(_vm.RestartNeeded);
    }

    [Fact]
    public async Task Language_change_is_saved_and_asks_for_a_restart()
    {
        await _vm.LoadAsync();
        _vm.SelectedLanguage = _vm.LanguageOptions.Single(o => o.Value == "hi");
        await Task.Delay(100);
        Assert.True(_vm.RestartNeeded);
        Assert.Equal("hi", await _db.Settings.GetAsync(SettingKeys.Language));
    }

    [Fact]
    public async Task Store_name_is_trimmed_and_saved()
    {
        await _vm.LoadAsync();
        _vm.StoreName = "  Shree Sarees ";
        await _vm.SaveStoreNameCommand.ExecuteAsync(null);
        Assert.Equal("Shree Sarees", await _db.Settings.GetAsync(SettingKeys.StoreName));
    }

    [Fact]
    public async Task Demo_data_can_be_loaded_and_removed()
    {
        await _vm.LoadAsync();
        await _vm.LoadDemoCommand.ExecuteAsync(null);
        Assert.True(_vm.DemoLoaded);
        await _vm.RemoveDemoAsync();
        Assert.False(_vm.DemoLoaded);
        Assert.Equal("60 demo contacts removed", _toast.Last!.Message);
    }
}
