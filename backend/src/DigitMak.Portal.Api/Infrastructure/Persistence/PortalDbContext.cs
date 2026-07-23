using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DigitMak.Portal.Api.Infrastructure.Persistence;

public class PortalDbContext(
    DbContextOptions<PortalDbContext> options,
    IHttpContextAccessor? httpContext = null
) : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IPortalDbContext
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<SubscriptionInvitation> SubscriptionInvitations => Set<SubscriptionInvitation>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<AccountChangeRequest> AccountChangeRequests => Set<AccountChangeRequest>();
    public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<ServiceCatalogueItem> ServiceCatalogueItems => Set<ServiceCatalogueItem>();
    public DbSet<ContentPage> ContentPages => Set<ContentPage>();
    public DbSet<Translation> Translations => Set<Translation>();
    public DbSet<EvidenceFile> EvidenceFiles => Set<EvidenceFile>();
    public DbSet<EvidenceTemplate> EvidenceTemplates => Set<EvidenceTemplate>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<FileObject> Files => Set<FileObject>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<AuditLog>().HasKey(x => x.Id);
        builder.Entity<Ticket>().HasIndex(x => x.TicketNumber).IsUnique();
        builder.Entity<TicketAttachment>().HasIndex(x => new { x.TicketId, x.FileId }).IsUnique();
        builder.Entity<EvidenceTemplate>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<AppUser>().HasIndex(x => x.OrganizationId);
        builder.Entity<AccountChangeRequest>().HasIndex(x => new { x.UserId, x.Status });
        builder
            .Entity<OrganizationMember>()
            .HasIndex(x => new { x.OrganizationId, x.UserId })
            .IsUnique();
        builder
            .Entity<Translation>()
            .HasIndex(x => new
            {
                x.EntityType,
                x.EntityId,
                x.Language,
                x.FieldName,
            })
            .IsUnique();
        builder.Entity<SystemSetting>().HasIndex(x => x.Key).IsUnique();
        builder.Entity<ServiceCatalogueItem>().HasIndex(x => x.Slug).IsUnique();
        builder.Entity<ContentPage>().HasIndex(x => x.Slug).IsUnique();

        ConfigureSqliteDates(builder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        PreserveAuditRecords();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (
            var entry in ChangeTracker.Entries<Entity>().Where(x => x.State == EntityState.Modified)
        )
            entry.Entity.UpdatedAt = now;

        foreach (
            var entry in ChangeTracker
                .Entries<AppUser>()
                .Where(x => x.State is EntityState.Added or EntityState.Modified)
        )
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = now;
            entry.Entity.UpdatedAt = now;
            SynchronizeEmailLifecycle(entry.Entity, now);
        }
    }

    private static void SynchronizeEmailLifecycle(AppUser user, DateTimeOffset now)
    {
        if (user.EmailConfirmed)
        {
            user.EmailVerifiedAt ??= now;
            if (user.Status == UserStatuses.PendingVerification)
                user.Status = UserStatuses.Active;
            return;
        }

        user.EmailVerifiedAt = null;
        if (user.Status == UserStatuses.Active)
            user.Status = UserStatuses.PendingVerification;
    }

    private void PreserveAuditRecords()
    {
        foreach (
            var entry in ChangeTracker.Entries<AuditLog>().Where(x => x.State == EntityState.Added)
        )
            entry.Entity.ActorIp ??=
                httpContext?.HttpContext?.Connection.RemoteIpAddress?.ToString();
        foreach (
            var entry in ChangeTracker
                .Entries<AuditLog>()
                .Where(x => x.State is EntityState.Modified or EntityState.Deleted)
        )
            entry.State = EntityState.Unchanged;
    }

    private void ConfigureSqliteDates(ModelBuilder builder)
    {
        if (Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite")
            return;

        var requiredConverter = new ValueConverter<DateTimeOffset, long>(
            value => value.UtcDateTime.Ticks,
            value => new DateTimeOffset(value, TimeSpan.Zero)
        );
        var optionalConverter = new ValueConverter<DateTimeOffset?, long?>(
            value => value.HasValue ? value.Value.UtcDateTime.Ticks : null,
            value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null
        );

        foreach (
            var property in builder
                .Model.GetEntityTypes()
                .SelectMany(entity => entity.GetProperties())
        )
        {
            if (property.ClrType == typeof(DateTimeOffset))
                property.SetValueConverter(requiredConverter);
            else if (property.ClrType == typeof(DateTimeOffset?))
                property.SetValueConverter(optionalConverter);
        }
    }
}
