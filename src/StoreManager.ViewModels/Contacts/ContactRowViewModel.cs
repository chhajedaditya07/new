using StoreManager.Core.Formatting;
using StoreManager.Core.Models;
using StoreManager.ViewModels.Infrastructure;

namespace StoreManager.ViewModels.Contacts;

public sealed record TagChipViewModel(Guid Id, string Name, string ColorHex);

public sealed class ContactRowViewModel
{
    public ContactRowViewModel(ContactListItem item, ILocalizer loc)
    {
        Id = item.Id;
        Name = item.Name;
        MobileDisplay = PhoneNumber.Display(item.Mobile);
        TypeLabel = loc.Get("ContactType_" + item.Type);
        City = item.City ?? "";
        Tags = item.Tags.Select(t => new TagChipViewModel(t.Id, t.Name, t.ColorHex)).ToList();
    }

    public Guid Id { get; }
    public string Name { get; }
    public string MobileDisplay { get; }
    public string TypeLabel { get; }
    public string City { get; }
    public IReadOnlyList<TagChipViewModel> Tags { get; }
}
