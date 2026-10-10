using StoreManager.Core.Entities;
using StoreManager.Core.Models;

namespace StoreManager.Core.Abstractions;

public interface IContactRepository
{
    Task<PagedResult<ContactListItem>> SearchAsync(ContactQuery query, CancellationToken ct = default);

    /// <summary>All matches without paging, for Excel export.</summary>
    Task<IReadOnlyList<Contact>> ListForExportAsync(ContactQuery query, CancellationToken ct = default);

    Task<Contact?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Looks a contact up by any phone format the user typed.</summary>
    Task<Contact?> FindByMobileAsync(string mobile, CancellationToken ct = default);

    /// <summary>Creates (Id not yet stored) or updates a contact and replaces its tags.</summary>
    Task<SaveResult> SaveAsync(Contact contact, IReadOnlyCollection<Guid> tagIds, CancellationToken ct = default);

    Task SoftDeleteAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    /// <summary>Restores deleted contacts; returns how many came back (a contact whose mobile was reused meanwhile stays deleted).</summary>
    Task<int> RestoreAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    Task AddTagsAsync(IReadOnlyCollection<Guid> contactIds, IReadOnlyCollection<Guid> tagIds, CancellationToken ct = default);
    Task RemoveTagsAsync(IReadOnlyCollection<Guid> contactIds, IReadOnlyCollection<Guid> tagIds, CancellationToken ct = default);

    /// <summary>Single-transaction bulk insert used by import. Rows with invalid or duplicate mobiles are skipped, not fatal.</summary>
    Task<BulkCreateResult> BulkCreateAsync(IReadOnlyList<BulkContact> rows, CancellationToken ct = default);
}

public interface ITagRepository
{
    Task<IReadOnlyList<TagInfo>> ListAsync(CancellationToken ct = default);
    Task<TagResult> CreateAsync(string name, string colorHex, CancellationToken ct = default);
    Task<TagResult> UpdateAsync(Guid id, string name, string colorHex, CancellationToken ct = default);
    Task<TagResult> MergeAsync(Guid sourceId, Guid targetId, CancellationToken ct = default);
    Task<TagResult> DeleteAsync(Guid id, CancellationToken ct = default);
}
