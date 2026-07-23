namespace DigitMak.Portal.Api.Infrastructure.Persistence.Repositories;

/// <summary>
/// Narrow, purpose-built data-access contracts for TicketService — one method per actual
/// query/write the service needs, instead of exposing the whole database shape (contrast with
/// IPortalDbContext, which stays available as a general-purpose escape hatch but is no longer
/// used by the ticket/meeting/contact-request services now that they have dedicated repositories).
/// </summary>
public interface ITicketRepository
{
    Task<AppUser?> FindUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> HasPortalAccessAsync(
        Guid userId,
        Guid organizationId,
        DateTimeOffset now,
        CancellationToken cancellationToken
    );
    Task<int> CountCreatedBetweenAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken
    );
    Task<Ticket?> FindAsync(Guid ticketId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Ticket>> ListCreatedByAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TicketMessage>> ListMessagesAsync(
        Guid ticketId,
        bool includeInternalNotes,
        CancellationToken cancellationToken
    );
    Task<IReadOnlyList<Guid>> GetStaffUserIdsAsync(CancellationToken cancellationToken);
    Task AddTicketAsync(
        Ticket ticket,
        IReadOnlyList<Notification> notifications,
        AuditLog auditLog,
        CancellationToken cancellationToken
    );
    Task AddMessageAsync(
        TicketMessage message,
        IEnumerable<Notification> notifications,
        AuditLog auditLog,
        CancellationToken cancellationToken
    );
}

public interface IMeetingRepository
{
    Task<AppUser?> FindUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> HasPortalAccessAsync(
        Guid userId,
        Guid organizationId,
        DateTimeOffset now,
        CancellationToken cancellationToken
    );
    Task<bool> OwnsTicketAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Meeting>> ListRequestedByAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Meeting>> ListCreatedByAsync(Guid userId, CancellationToken cancellationToken);
    Task AddAsync(
        Meeting meeting,
        Notification notification,
        AuditLog auditLog,
        CancellationToken cancellationToken
    );
}

public interface IContactRequestRepository
{
    Task<IReadOnlyList<Guid>> GetAdminUserIdsAsync(CancellationToken cancellationToken);

    Task AddAsync(
        ContactRequest request,
        IReadOnlyList<Notification> notifications,
        AuditLog auditLog,
        CancellationToken cancellationToken
    );
}

public interface IPublicContentRepository
{
    Task<IReadOnlyList<ServiceCatalogueItem>> ListPublishedServicesAsync(
        CancellationToken cancellationToken
    );
    Task<ContentPage?> FindPublishedPageAsync(string slug, CancellationToken cancellationToken);
    Task<IReadOnlyList<Translation>> ListTranslationsAsync(
        string entityType,
        string language,
        CancellationToken cancellationToken
    );
}
