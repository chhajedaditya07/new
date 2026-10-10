using StoreManager.Core.Entities;
using StoreManager.ViewModels.Contacts;

namespace StoreManager.Tests.ViewModels;

public class ContactPanelViewModelTests : IAsyncLifetime
{
    private TestDb _db = null!;
    private FakeToast _toast = null!;
    private FakePlatform _platform = null!;
    private ContactPanelViewModel _vm = null!;

    public async Task InitializeAsync()
    {
        _db = await TestDb.CreateAsync();
        _toast = new FakeToast();
        _platform = new FakePlatform();
        _vm = new ContactPanelViewModel(_db.Contacts, _db.Tags, _toast, new ReswLocalizer(), _platform) { DuplicateCheckDelay = TimeSpan.Zero };
    }
    public Task DisposeAsync() { _db.Dispose(); return Task.CompletedTask; }

    private async Task<Contact> Existing(string name = "Aarti Sharma", string mobile = "9876543210")
        => (await _db.Contacts.SaveAsync(new Contact { Name = name, Mobile = mobile, City = "Jaipur", Address = "12 MI Road" }, [])).Contact!;

    [Fact]
    public async Task New_contact_opens_an_empty_edit_form()
    {
        await _vm.BeginNewAsync();
        Assert.True(_vm.IsEditing);
        Assert.Equal("New contact", _vm.Title);
        Assert.Equal("", _vm.Name);
        Assert.Equal(ContactType.Customer, _vm.SelectedType.Value);
    }

    [Fact]
    public async Task Saving_an_empty_form_shows_localized_inline_errors_and_stays_in_edit_mode()
    {
        await _vm.BeginNewAsync();
        await _vm.SaveAsync();
        Assert.True(_vm.IsEditing);
        Assert.Equal("Please enter a name.", _vm.NameError);
        Assert.Equal("Please enter a mobile number.", _vm.MobileError);
        Assert.True(_vm.HasNameError && _vm.HasMobileError);
        Assert.Empty(_toast.Shown);
    }

    [Fact]
    public async Task Typing_clears_that_fields_error()
    {
        await _vm.BeginNewAsync();
        await _vm.SaveAsync();
        _vm.Name = "A";
        Assert.False(_vm.HasNameError);
        Assert.True(_vm.HasMobileError);
    }

    [Fact]
    public async Task Invalid_mobile_gets_a_specific_message()
    {
        await _vm.BeginNewAsync();
        _vm.Name = "Aarti"; _vm.Mobile = "12345";
        await _vm.SaveAsync();
        Assert.Equal("Enter a valid 10-digit mobile number.", _vm.MobileError);
    }

    [Fact]
    public async Task Valid_save_switches_to_profile_toasts_and_raises_Saved()
    {
        Guid? saved = null;
        _vm.Saved += (_, id) => saved = id;
        await _vm.BeginNewAsync();
        _vm.Name = "  Aarti Sharma ";
        _vm.Mobile = "98765 43210";
        _vm.City = "Jaipur";
        _vm.Birthday = new DateTimeOffset(1990, 8, 15, 0, 0, 0, TimeSpan.Zero);
        await _vm.SaveAsync();

        Assert.True(_vm.IsViewing);
        Assert.NotNull(saved);
        Assert.Equal("Aarti Sharma saved", _toast.Last!.Message);
        Assert.Equal("Aarti Sharma", _vm.Title);
        Assert.Equal("+91 98765 43210", _vm.MobileDisplay);
        Assert.Equal("15-08-1990", _vm.BirthdayText);
        Assert.Equal("Customer", _vm.TypeText);
        Assert.Equal("₹0", _vm.BalanceText);
    }

    [Fact]
    public async Task Typing_an_existing_mobile_warns_and_can_open_that_contact()
    {
        var existing = await Existing();
        Guid? opened = null;
        _vm.OpenContactRequested += (_, id) => opened = id;

        await _vm.BeginNewAsync();
        _vm.Mobile = "98765-43210";
        await Task.Delay(50);                           // the check is fire-and-forget

        Assert.True(_vm.HasDuplicateWarning);
        Assert.Equal("Aarti Sharma already has this mobile number.", _vm.DuplicateWarning);
        Assert.Equal(existing.Id, _vm.DuplicateContactId);

        _vm.OpenDuplicateCommand.Execute(null);
        Assert.Equal(existing.Id, opened);
    }

    [Fact]
    public async Task Editing_a_contact_does_not_warn_about_its_own_number()
    {
        var c = await Existing();
        await _vm.ShowAsync(c.Id);
        await _vm.EditAsync();
        _vm.Mobile = "+91 98765 43210";
        await Task.Delay(50);
        Assert.False(_vm.HasDuplicateWarning);
    }

    [Fact]
    public async Task Saving_a_duplicate_is_blocked_with_a_clear_message()
    {
        await Existing();
        await _vm.BeginNewAsync();
        _vm.Name = "Someone else"; _vm.Mobile = "9876543210";
        await _vm.SaveAsync();
        Assert.True(_vm.IsEditing);
        Assert.Equal("This mobile number is already saved.", _vm.MobileError);
        Assert.NotNull(_vm.DuplicateContactId);
    }

    [Fact]
    public async Task Cancel_after_editing_discards_unsaved_changes()
    {
        var c = await Existing();
        await _vm.ShowAsync(c.Id);
        await _vm.EditAsync();
        _vm.Name = "Changed but not saved";
        _vm.City = "Nowhere";
        await _vm.CancelAsync();

        Assert.True(_vm.IsViewing);
        Assert.Equal("Aarti Sharma", _vm.Name);
        Assert.Equal("Jaipur", _vm.City);
        Assert.Equal("Aarti Sharma", _vm.Title);
    }

    [Fact]
    public async Task Cancel_on_a_new_contact_closes_the_panel()
    {
        await _vm.BeginNewAsync();
        await _vm.CancelAsync();
        Assert.False(_vm.IsOpen);
    }

    [Fact]
    public async Task Editing_saves_changes_and_tags()
    {
        var vip = (await _db.Tags.CreateAsync("VIP", "#6B4FBB")).Tag!;
        var c = await Existing();
        await _vm.ShowAsync(c.Id);
        await _vm.EditAsync();
        _vm.Name = "Aarti Jain";
        _vm.TagChoices.Single(t => t.Id == vip.Id).IsSelected = true;
        await _vm.SaveAsync();

        Assert.True(_vm.IsViewing);
        Assert.Equal("Aarti Jain", _vm.Title);
        Assert.Equal(["VIP"], _vm.ProfileTags.Select(t => t.Name).ToArray());
    }

    [Fact]
    public async Task System_tags_are_not_offered_in_the_form()
    {
        await _db.Demo.LoadAsync(3);                    // creates the system "Demo data" tag plus ordinary demo tags
        await _vm.BeginNewAsync();
        Assert.NotEmpty(_vm.TagChoices);
        Assert.DoesNotContain(_vm.TagChoices, t => t.Name == StoreManager.Core.Entities.SystemTags.Demo);
    }

    [Fact]
    public async Task Phone_fields_are_auto_formatted_but_invalid_text_is_left_alone()
    {
        await _vm.BeginNewAsync();
        _vm.Mobile = "9876543210"; _vm.AltPhone = "abc";
        _vm.FormatPhoneFields();
        Assert.Equal("+91 98765 43210", _vm.Mobile);
        Assert.Equal("abc", _vm.AltPhone);
    }

    [Fact]
    public async Task Copy_number_and_address_use_the_clipboard()
    {
        var c = await Existing();
        await _vm.ShowAsync(c.Id);
        await _vm.CopyNumberCommand.ExecuteAsync(null);
        await _vm.CopyAddressCommand.ExecuteAsync(null);
        Assert.Equal(["+919876543210", "12 MI Road, Jaipur"], _platform.Copied);
    }

    [Fact]
    public async Task Copy_address_without_one_shows_a_warning()
    {
        var c = (await _db.Contacts.SaveAsync(new Contact { Name = "No Address", Mobile = "9123456780" }, [])).Contact!;
        await _vm.ShowAsync(c.Id);
        await _vm.CopyAddressCommand.ExecuteAsync(null);
        Assert.Empty(_platform.Copied);
        Assert.Equal(StoreManager.ViewModels.Infrastructure.ToastKind.Warning, _toast.Last!.Kind);
    }

    [Fact]
    public async Task WhatsApp_prefers_desktop_then_falls_back_to_the_browser_link()
    {
        var c = (await _db.Contacts.SaveAsync(new Contact { Name = "A", Mobile = "9876543210", WhatsApp = "9123456780" }, [])).Contact!;
        await _vm.ShowAsync(c.Id);

        await _vm.OpenWhatsAppCommand.ExecuteAsync(null);
        Assert.Equal("whatsapp", Assert.Single(_platform.Opened).Scheme);
        Assert.Contains("phone=919123456780", _platform.Opened[0].OriginalString);   // uses the WhatsApp number, not the mobile

        _platform.Opened.Clear();
        _platform.DesktopWhatsAppInstalled = false;
        await _vm.OpenWhatsAppCommand.ExecuteAsync(null);
        Assert.Equal(["whatsapp", "https"], _platform.Opened.Select(u => u.Scheme).ToArray());
        Assert.StartsWith("https://wa.me/919123456780", _platform.Opened[1].OriginalString);
    }

    [Fact]
    public async Task Delete_request_is_forwarded_for_the_open_contact()
    {
        var c = await Existing();
        Guid? requested = null;
        _vm.DeleteRequested += (_, id) => requested = id;
        await _vm.ShowAsync(c.Id);
        _vm.RequestDeleteCommand.Execute(null);
        Assert.Equal(c.Id, requested);
    }
}
