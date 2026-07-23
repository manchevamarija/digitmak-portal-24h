using Microsoft.EntityFrameworkCore;

namespace DigitMak.Portal.Api.Infrastructure.Persistence;

/// <summary>
/// The slice of PortalDbContext that Service-layer classes are allowed to see when a
/// dedicated repository would be overkill. PortalDbContext implements this interface;
/// TicketService/MeetingService/ContactRequestService/PublicContentService use the
/// narrower ITicketRepository/IMeetingRepository/IContactRequestRepository/
/// IPublicContentRepository instead — this stays as a general-purpose fallback.
/// </summary>
public interface IPortalDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<Organization> Organizations { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<ContactRequest> ContactRequests { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<TicketMessage> TicketMessages { get; }
    DbSet<Meeting> Meetings { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity> Add<TEntity>(TEntity entity)
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
