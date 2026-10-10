using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoreManager.Core.Abstractions;
using StoreManager.Core.Models;
using StoreManager.Data.Repositories;
using StoreManager.ViewModels.Infrastructure;

namespace StoreManager.ViewModels.Tags;

/// <summary>A selectable colour in the tag palette.</summary>
public sealed record Swatch(string Hex);

public sealed partial class TagRowViewModel : ObservableObject
{
    private readonly string _originalName;
    private readonly string _originalColor;

    public TagRowViewModel(TagInfo info)
    {
        Id = info.Id;
        IsSystem = info.IsSystem;
        ContactCount = info.ContactCount;
        _name = _originalName = info.Name;
        _colorHex = _originalColor = info.ColorHex;
    }

    public Guid Id { get; }
    public bool IsSystem { get; }
    public int ContactCount { get; }
    public bool CanEdit => !IsSystem;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsDirty))] private string _name;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsDirty))] private string _colorHex;

    public bool IsDirty => !IsSystem && (Name.Trim() != _originalName || !string.Equals(ColorHex, _originalColor, StringComparison.OrdinalIgnoreCase));
}

public sealed partial class TagManagerViewModel : ObservableObject
{
    private readonly ITagRepository _tags;
    private readonly IToastService _toast;
    private readonly ILocalizer _loc;

    public TagManagerViewModel(ITagRepository tags, IToastService toast, ILocalizer loc)
    {
        _tags = tags; _toast = toast; _loc = loc;
        _newColor = Palette[3];
    }

    public static IReadOnlyList<string> Palette => TagColors.Palette;
    public static IReadOnlyList<Swatch> Swatches { get; } = TagColors.Palette.Select(h => new Swatch(h)).ToList();

    public ObservableCollection<TagRowViewModel> Rows { get; } = new();

    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private string _newColor;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasError))] private string _error = "";
    [ObservableProperty] private bool _isEmpty;

    public bool HasError => Error.Length > 0;

    [RelayCommand]
    public async Task LoadAsync()
    {
        var list = await _tags.ListAsync();
        Rows.Clear();
        foreach (var t in list) Rows.Add(new TagRowViewModel(t));
        IsEmpty = Rows.Count == 0;
    }

    [RelayCommand]
    public async Task CreateAsync()
    {
        Error = "";
        var r = await _tags.CreateAsync(NewName, NewColor);
        if (!r.Success) { Error = _loc.Get(Messages.TagError(r.ErrorCode)); return; }
        _toast.Show(_loc.Format("Toast_TagCreated", r.Tag!.Name));
        NewName = "";
        await LoadAsync();
    }

    public async Task<bool> SaveRowAsync(TagRowViewModel row)
    {
        Error = "";
        var r = await _tags.UpdateAsync(row.Id, row.Name, row.ColorHex);
        if (!r.Success)
        {
            Error = _loc.Get(Messages.TagError(r.ErrorCode));
            await LoadAsync();            // revert the edited row to what is stored
            return false;
        }
        _toast.Show(_loc.Format("Toast_TagSaved", r.Tag!.Name));
        await LoadAsync();
        return true;
    }

    public async Task<bool> MergeAsync(Guid sourceId, Guid targetId)
    {
        Error = "";
        var r = await _tags.MergeAsync(sourceId, targetId);
        if (!r.Success) { Error = _loc.Get(Messages.TagError(r.ErrorCode)); return false; }
        _toast.Show(_loc.Format("Toast_TagMerged", r.Tag!.Name));
        await LoadAsync();
        return true;
    }

    /// <summary>The view asks the user to confirm first (this is a destructive action).</summary>
    public async Task<bool> DeleteAsync(Guid id)
    {
        Error = "";
        var r = await _tags.DeleteAsync(id);
        if (!r.Success) { Error = _loc.Get(Messages.TagError(r.ErrorCode)); return false; }
        _toast.Show(_loc.Get("Toast_TagDeleted"), ToastKind.Info);
        await LoadAsync();
        return true;
    }
}
