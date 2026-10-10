using CommunityToolkit.Mvvm.ComponentModel;
using StoreManager.Core.Entities;

namespace StoreManager.ViewModels.Contacts;

public sealed partial class TypeFilterOption : ObservableObject
{
    public TypeFilterOption(ContactType type, string label) { Type = type; Label = label; }
    public ContactType Type { get; }
    public string Label { get; }
    [ObservableProperty] private bool _isSelected;
}

public sealed partial class TagFilterOption : ObservableObject
{
    public TagFilterOption(Guid id, string name, string colorHex, int count, bool isSystem = false)
    {
        Id = id; Name = name; ColorHex = colorHex; Count = count; IsSystem = isSystem;
    }
    public bool IsSystem { get; }
    public Guid Id { get; }
    public string Name { get; }
    public string ColorHex { get; }
    public int Count { get; }
    public string Display => $"{Name} ({Count})";
    [ObservableProperty] private bool _isSelected;
}
