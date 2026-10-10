using StoreManager.Core.Entities;

namespace StoreManager.Core.Models;

public enum TagMatchMode { Any, All }

public enum ContactSort { NameAsc, NameDesc, NewestFirst, CityAsc }

public sealed record ContactQuery(
    string? Text = null,
    IReadOnlyCollection<ContactType>? Types = null,
    IReadOnlyCollection<Guid>? TagIds = null,
    TagMatchMode TagMatch = TagMatchMode.Any,
    ContactSort Sort = ContactSort.NameAsc,
    int Skip = 0,
    int Take = 50);

public sealed record TagChip(Guid Id, string Name, string ColorHex);

public sealed record ContactListItem(
    Guid Id, string Name, string Mobile, ContactType Type, string? City, IReadOnlyList<TagChip> Tags);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);

public sealed record TagInfo(Guid Id, string Name, string ColorHex, bool IsSystem, int ContactCount);

/// <summary>Outcome of a save. <see cref="Errors"/> are field-level; <see cref="ExistingContactId"/> is set on duplicate mobile.</summary>
public sealed record SaveResult(
    bool Success,
    Contact? Contact = null,
    IReadOnlyList<Validation.ValidationError>? Errors = null,
    Guid? ExistingContactId = null)
{
    public const string DuplicateMobile = "Duplicate";
}

public sealed record TagResult(bool Success, string? ErrorCode = null, Tag? Tag = null);

public sealed record BulkContact(Contact Contact, IReadOnlyList<string> TagNames);

public sealed record BulkCreateResult(int Created, IReadOnlyList<(int Index, string Reason)> Skipped);
