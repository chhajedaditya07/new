using Microsoft.EntityFrameworkCore;
using StoreManager.Core.Abstractions;
using StoreManager.Core.Entities;

namespace StoreManager.Data;

public class AppDbContext : DbContext
{
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, IClock clock, ICurrentUser currentUser)
        : base(options)
    {
        _clock = clock;
        _currentUser = currentUser;
    }

    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ContactTag> ContactTags => Set<ContactTag>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Contact>(e =>
        {
            e.ToTable("Contacts");
            e.Property(x => x.Name).HasMaxLength(200).UseCollation("NOCASE");
            e.Property(x => x.Mobile).HasMaxLength(20);
            e.Property(x => x.WhatsApp).HasMaxLength(20);
            e.Property(x => x.AltPhone).HasMaxLength(20);
            e.Property(x => x.City).HasMaxLength(100).UseCollation("NOCASE");
            e.Property(x => x.Gstin).HasMaxLength(15);
            e.HasQueryFilter(x => !x.IsDeleted);
            // One live contact per mobile; a soft-deleted contact does not block re-use of the number.
            e.HasIndex(x => x.Mobile).IsUnique().HasFilter("\"IsDeleted\" = 0");
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.Type);
            e.HasIndex(x => x.AltPhone);
            e.HasIndex(x => x.IsDeleted);
            e.HasOne<Contact>().WithMany().HasForeignKey(x => x.ReferredByContactId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Tag>(e =>
        {
            e.ToTable("Tags");
            e.Property(x => x.Name).HasMaxLength(60).UseCollation("NOCASE");
            e.Property(x => x.ColorHex).HasMaxLength(7);
            e.HasIndex(x => x.Name).IsUnique();   // tags are hard-deleted, so no filter is needed
        });

        b.Entity<ContactTag>(e =>
        {
            e.ToTable("ContactTags");
            e.HasKey(x => new { x.ContactId, x.TagId });
            e.HasOne(x => x.Contact).WithMany(c => c.ContactTags).HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Tag).WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TagId, x.ContactId });
            e.HasQueryFilter(x => !x.Contact.IsDeleted);
        });

        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        b.Entity<Setting>(e =>
        {
            e.ToTable("Settings");
            e.HasKey(x => x.Key);
        });

        b.Entity<ActivityLog>(e =>
        {
            e.ToTable("ActivityLogs");
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.At);
        });

        b.Entity<Attachment>(e =>
        {
            e.ToTable("Attachments");
            e.HasIndex(x => new { x.OwnerType, x.OwnerId });
            e.HasQueryFilter(x => !x.IsDeleted);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAuditFields();
        return base.SaveChanges();
    }

    private void StampAuditFields()
    {
        var now = _clock.UtcNow;
        var user = _currentUser.UserId;
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.CreatedBy ??= user;
                    entry.Entity.UpdatedBy = user;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = user;
                    entry.Property(nameof(EntityBase.CreatedAt)).IsModified = false;
                    entry.Property(nameof(EntityBase.CreatedBy)).IsModified = false;
                    break;
            }
        }
    }
}
