using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoreManager.Core.Abstractions;
using StoreManager.Core.Entities;
using StoreManager.Core.Formatting;
using StoreManager.Core.Models;
using StoreManager.Services.Messaging;
using StoreManager.ViewModels.Infrastructure;

namespace StoreManager.ViewModels.Contacts;

public enum PanelMode { None, View, Edit }

public sealed partial class TagChoice : ObservableObject
{
    public TagChoice(Guid id, string name, string colorHex, bool selected)
    {
        Id = id; Name = name; ColorHex = colorHex; _isSelected = selected;
    }
    public Guid Id { get; }
    public string Name { get; }
    public string ColorHex { get; }
    [ObservableProperty] private bool _isSelected;
}

/// <summary>The side panel: shows a contact's profile and hosts the add/edit form, so the user never leaves the list.</summary>
public sealed partial class ContactPanelViewModel : ObservableObject
{
    private readonly IContactRepository _contacts;
    private readonly ITagRepository _tags;
    private readonly IToastService _toast;
    private readonly ILocalizer _loc;
    private readonly IPlatformActions _platform;
    private CancellationTokenSource? _dupCts;
    private Guid _editingId;
    private bool _isNew;

    public ContactPanelViewModel(IContactRepository contacts, ITagRepository tags, IToastService toast,
        ILocalizer loc, IPlatformActions platform)
    {
        _contacts = contacts; _tags = tags; _toast = toast; _loc = loc; _platform = platform;
        TypeOptions = Enum.GetValues<ContactType>()
            .Select(t => new Option<ContactType>(t, loc.Get("ContactType_" + t))).ToList();
        _selectedType = TypeOptions[0];
    }

    /// <summary>Delay before the duplicate-mobile check runs after typing. Tests set this to zero.</summary>
    public TimeSpan DuplicateCheckDelay { get; set; } = TimeSpan.FromMilliseconds(300);

    public event EventHandler<Guid>? Saved;
    public event EventHandler<Guid>? DeleteRequested;
    public event EventHandler<Guid>? OpenContactRequested;

    public IReadOnlyList<Option<ContactType>> TypeOptions { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsOpen), nameof(IsViewing), nameof(IsEditing), nameof(Title))]
    private PanelMode _mode = PanelMode.None;

    public bool IsOpen => Mode != PanelMode.None;
    public bool IsViewing => Mode == PanelMode.View;
    public bool IsEditing => Mode == PanelMode.Edit;
    public string Title => Mode == PanelMode.Edit ? _loc.Get(_isNew ? "Panel_NewContact" : "Panel_EditContact") : Name;

    // ---------- form fields ----------
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _mobile = "";
    [ObservableProperty] private string _whatsApp = "";
    [ObservableProperty] private string _altPhone = "";
    [ObservableProperty] private string _city = "";
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _gstin = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private DateTimeOffset? _birthday;
    [ObservableProperty] private DateTimeOffset? _anniversary;
    [ObservableProperty] private Option<ContactType> _selectedType;

    public List<TagChoice> TagChoices { get; private set; } = new();

    // ---------- inline validation ----------
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasNameError))] private string _nameError = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasMobileError))] private string _mobileError = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasWhatsAppError))] private string _whatsAppError = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasAltPhoneError))] private string _altPhoneError = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasGstinError))] private string _gstinError = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasDuplicateWarning))] private string _duplicateWarning = "";
    [ObservableProperty] private Guid? _duplicateContactId;

    public bool HasNameError => NameError.Length > 0;
    public bool HasMobileError => MobileError.Length > 0;
    public bool HasWhatsAppError => WhatsAppError.Length > 0;
    public bool HasAltPhoneError => AltPhoneError.Length > 0;
    public bool HasGstinError => GstinError.Length > 0;
    public bool HasDuplicateWarning => DuplicateWarning.Length > 0;

    partial void OnNameChanged(string value) => NameError = "";
    partial void OnWhatsAppChanged(string value) => WhatsAppError = "";
    partial void OnAltPhoneChanged(string value) => AltPhoneError = "";
    partial void OnGstinChanged(string value) => GstinError = "";

    partial void OnMobileChanged(string value)
    {
        MobileError = "";
        _ = CheckDuplicateAsync(value);
    }

    private async Task CheckDuplicateAsync(string raw)
    {
        _dupCts?.Cancel();
        var cts = _dupCts = new CancellationTokenSource();
        DuplicateWarning = "";
        DuplicateContactId = null;
        if (!IsEditing || !PhoneNumber.TryNormalize(raw, out _)) return;
        try
        {
            if (DuplicateCheckDelay > TimeSpan.Zero) await Task.Delay(DuplicateCheckDelay, cts.Token);
            var found = await _contacts.FindByMobileAsync(raw, cts.Token);
            if (cts.IsCancellationRequested) return;
            if (found is not null && found.Id != _editingId)
            {
                DuplicateContactId = found.Id;
                DuplicateWarning = _loc.Format("Warn_MobileExists", found.Name);
            }
        }
        catch (OperationCanceledException) { /* superseded by newer keystroke */ }
    }

    // ---------- profile (read-only) ----------
    [ObservableProperty] private Guid _currentId;
    [ObservableProperty] private string _typeText = "";
    [ObservableProperty] private string _mobileDisplay = "";
    [ObservableProperty] private string _whatsAppDisplay = "";
    [ObservableProperty] private string _altPhoneDisplay = "";
    [ObservableProperty] private string _birthdayText = "";
    [ObservableProperty] private string _anniversaryText = "";
    [ObservableProperty] private string _balanceText = Money.Format(0);
    [ObservableProperty] private IReadOnlyList<TagChipViewModel> _profileTags = Array.Empty<TagChipViewModel>();
    [ObservableProperty] private string _referredByName = "";

    /// <summary>Receivable minus payable for this contact. Wired to the Payments module in step (d); shows ₹0 until then.</summary>
    public string BalanceCaption => _loc.Get("Panel_Balance");

    public bool HasCity => City.Length > 0;
    public bool HasAddress => Address.Length > 0;
    public bool HasGstin => Gstin.Length > 0;
    public bool HasNotes => Notes.Length > 0;
    public bool HasAltPhone => AltPhoneDisplay.Length > 0;
    public bool HasBirthday => BirthdayText.Length > 0;
    public bool HasAnniversary => AnniversaryText.Length > 0;

    // ---------- opening / closing ----------

    public async Task ShowAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _contacts.GetAsync(id, ct);
        if (c is null) { Close(); return; }
        LoadFrom(c);
        FillProfile(c);
        Mode = PanelMode.View;
    }

    public async Task BeginNewAsync(string? prefillMobile = null)
    {
        _isNew = true;
        _editingId = Guid.CreateVersion7();
        LoadFrom(new Contact { Id = _editingId, Mobile = prefillMobile ?? "" });
        await LoadTagChoicesAsync(Array.Empty<Guid>());
        Mode = PanelMode.Edit;
        OnPropertyChanged(nameof(Title));
        if (!string.IsNullOrEmpty(prefillMobile)) _ = CheckDuplicateAsync(prefillMobile);
    }

    [RelayCommand]
    public async Task EditAsync()
    {
        var c = await _contacts.GetAsync(CurrentId);
        if (c is null) { Close(); return; }
        _isNew = false;
        LoadFrom(c);
        await LoadTagChoicesAsync(c.ContactTags.Select(t => t.TagId).ToList());
        Mode = PanelMode.Edit;
        OnPropertyChanged(nameof(Title));
    }

    /// <summary>Discards unsaved edits: a new contact closes the panel, an existing one reloads from storage.</summary>
    [RelayCommand]
    public async Task CancelAsync()
    {
        if (_isNew || CurrentId == Guid.Empty) Close();
        else await ShowAsync(CurrentId);
    }

    [RelayCommand]
    public void Close()
    {
        Mode = PanelMode.None;
        ClearErrors();
    }

    private void LoadFrom(Contact c)
    {
        _editingId = c.Id;
        CurrentId = _isNew ? Guid.Empty : c.Id;
        ClearErrors();
        Name = c.Name;
        Mobile = c.Mobile;
        WhatsApp = c.WhatsApp ?? "";
        AltPhone = c.AltPhone ?? "";
        City = c.City ?? "";
        Address = c.Address ?? "";
        Gstin = c.Gstin ?? "";
        Notes = c.Notes ?? "";
        Birthday = c.Birthday is { } b ? new DateTimeOffset(b.ToDateTime(TimeOnly.MinValue)) : null;
        Anniversary = c.Anniversary is { } a ? new DateTimeOffset(a.ToDateTime(TimeOnly.MinValue)) : null;
        SelectedType = TypeOptions.First(o => o.Value == c.Type);
        _referredBy = c.ReferredByContactId;
        _photo = c.PhotoAttachmentId;
    }

    private Guid? _referredBy;
    private Guid? _photo;

    private void FillProfile(Contact c)
    {
        _isNew = false;
        CurrentId = c.Id;
        TypeText = _loc.Get("ContactType_" + c.Type);
        MobileDisplay = PhoneNumber.Display(c.Mobile);
        WhatsAppDisplay = PhoneNumber.Display(c.EffectiveWhatsApp);
        AltPhoneDisplay = PhoneNumber.Display(c.AltPhone);
        BirthdayText = IstDate.Date(c.Birthday);
        AnniversaryText = IstDate.Date(c.Anniversary);
        ProfileTags = c.ContactTags.Select(t => new TagChipViewModel(t.TagId, t.Tag.Name, t.Tag.ColorHex))
            .OrderBy(t => t.Name).ToList();
        foreach (var n in new[] { nameof(HasCity), nameof(HasAddress), nameof(HasGstin), nameof(HasNotes),
                     nameof(HasAltPhone), nameof(HasBirthday), nameof(HasAnniversary), nameof(Title) })
            OnPropertyChanged(n);
    }

    private async Task LoadTagChoicesAsync(IReadOnlyCollection<Guid> selected)
    {
        var all = await _tags.ListAsync();
        TagChoices = all.Where(t => !t.IsSystem)
            .Select(t => new TagChoice(t.Id, t.Name, t.ColorHex, selected.Contains(t.Id))).ToList();
        OnPropertyChanged(nameof(TagChoices));
    }

    private void ClearErrors()
    {
        NameError = MobileError = WhatsAppError = AltPhoneError = GstinError = "";
        DuplicateWarning = "";
        DuplicateContactId = null;
    }

    /// <summary>Called when a phone box loses focus: "9876543210" becomes "+91 98765 43210". Invalid text is left for the user to fix.</summary>
    public void FormatPhoneFields()
    {
        string Fmt(string raw) => PhoneNumber.TryNormalize(raw, out var n) ? PhoneNumber.Display(n) : raw;
        if (Mobile.Length > 0) Mobile = Fmt(Mobile);
        if (WhatsApp.Length > 0) WhatsApp = Fmt(WhatsApp);
        if (AltPhone.Length > 0) AltPhone = Fmt(AltPhone);
    }

    // ---------- save ----------

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (!IsEditing) return;
        ClearErrors();

        var contact = new Contact
        {
            Id = _editingId,
            Type = SelectedType.Value,
            Name = Name,
            Mobile = Mobile,
            WhatsApp = WhatsApp,
            AltPhone = AltPhone,
            City = City,
            Address = Address,
            Gstin = Gstin,
            Notes = Notes,
            Birthday = Birthday is { } b ? DateOnly.FromDateTime(b.DateTime) : null,
            Anniversary = Anniversary is { } a ? DateOnly.FromDateTime(a.DateTime) : null,
            ReferredByContactId = _referredBy,
            PhotoAttachmentId = _photo,
        };
        var tagIds = TagChoices.Where(t => t.IsSelected).Select(t => t.Id).ToList();

        var result = await _contacts.SaveAsync(contact, tagIds);
        if (!result.Success)
        {
            foreach (var e in result.Errors ?? [])
            {
                var text = _loc.Get(Messages.KeyFor(e));
                switch (e.Field)
                {
                    case "Name": NameError = text; break;
                    case "Mobile": MobileError = text; break;
                    case "WhatsApp": WhatsAppError = text; break;
                    case "AltPhone": AltPhoneError = text; break;
                    case "Gstin": GstinError = text; break;
                }
            }
            if (result.ExistingContactId is { } existing)
            {
                DuplicateContactId = existing;
                var found = await _contacts.GetAsync(existing);
                if (found is not null) DuplicateWarning = _loc.Format("Warn_MobileExists", found.Name);
            }
            return;
        }

        _toast.Show(_loc.Format("Toast_ContactSaved", result.Contact!.Name));
        _isNew = false;
        await ShowAsync(result.Contact.Id);
        Saved?.Invoke(this, result.Contact.Id);
    }

    [RelayCommand]
    private void OpenDuplicate()
    {
        if (DuplicateContactId is { } id) OpenContactRequested?.Invoke(this, id);
    }

    [RelayCommand]
    private void RequestDelete()
    {
        if (CurrentId != Guid.Empty) DeleteRequested?.Invoke(this, CurrentId);
    }

    // ---------- one-click actions ----------

    [RelayCommand]
    private async Task CopyNumberAsync()
    {
        var c = await _contacts.GetAsync(CurrentId);
        if (c is null) return;
        await _platform.CopyTextAsync(c.Mobile);
        _toast.Show(_loc.Format("Toast_NumberCopied", PhoneNumber.Display(c.Mobile)), ToastKind.Info);
    }

    [RelayCommand]
    private async Task CopyAddressAsync()
    {
        var c = await _contacts.GetAsync(CurrentId);
        if (c is null) return;
        var text = string.Join(", ", new[] { c.Address, c.City }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (text.Length == 0) { _toast.Show(_loc.Get("Toast_NoAddress"), ToastKind.Warning); return; }
        await _platform.CopyTextAsync(text);
        _toast.Show(_loc.Get("Toast_AddressCopied"), ToastKind.Info);
    }

    [RelayCommand]
    private async Task OpenWhatsAppAsync()
    {
        var c = await _contacts.GetAsync(CurrentId);
        if (c is null) return;
        var phone = c.EffectiveWhatsApp;
        // Prefer WhatsApp Desktop; fall back to the browser link. The user presses Send themselves.
        if (!await _platform.OpenUriAsync(WhatsAppLinks.Desktop(phone, "")))
            await _platform.OpenUriAsync(WhatsAppLinks.Web(phone, ""));
    }
}
