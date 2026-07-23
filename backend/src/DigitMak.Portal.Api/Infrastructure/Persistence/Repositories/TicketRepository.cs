using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;
using Microsoft.EntityFrameworkCore;

namespace DigitMak.Portal.Api.Infrastructure.Persistence.Repositories;

public sealed class TicketRepository(PortalDbContext db) : ITicketRepository
{
    public Task<AppUser?> FindUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public async Task<bool> HasPortalAccessAsync(
        Guid userId,
        Guid organizationId,
        DateTimeOffset now,
        CancellationToken cancellationToken
    ) =>
        await db.Organizations.AnyAsync(
            organization => organization.Id == organizationId && organization.Status == "Approved",
            cancellationToken
        )
        && await db.Subscriptions.AnyAsync(
            subscription =>
                subscription.UserId == userId
                && subscription.Status == "Active"
                && subscription.ExpiresAt > now,
            cancellationToken
        );

    public Task<int> CountCreatedBetweenAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken
    ) => db.Tickets.CountAsync(ticket => ticket.CreatedAt >= from && ticket.CreatedAt < to, cancellationToken);

    public Task<Ticket?> FindAsync(Guid ticketId, CancellationToken cancellationToken) =>
        db.Tickets.SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);

    public async Task<IReadOnlyList<Ticket>> ListCreatedByAsync(Guid userId, CancellationToken cancellationToken) =>
        await db
            .Tickets.Where(ticket => ticket.CreatedByUserId == userId)
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TicketMessage>> ListMessagesAsync(
        Guid ticketId,
        bool includeInternalNotes,
        CancellationToken cancellationToken
    ) =>
        await db
            .TicketMessages.Where(message =>
                message.TicketId == ticketId && (message.MessageType != "InternalNote" || includeInternalNotes)
            )
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetStaffUserIdsAsync(CancellationToken cancellationToken) =>
        await db
            .UserRoles.Join(
                db.Roles.Where(role => role.Name == "Admin" || role.Name == "HelpDeskAgent"),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, _) => userRole.UserId
            )
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task AddTicketAsync(
        Ticket ticket,
        IReadOnlyList<Notification> notifications,
        AuditLog auditLog,
        CancellationToken cancellationToken
    )
    {
        db.Tickets.Add(ticket);
        db.Notifications.AddRange(notifications);
        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddMessageAsync(
        TicketMessage message,
        IEnumerable<Notification> notifications,
        AuditLog auditLog,
        CancellationToken cancellationToken
    )
    {
        db.TicketMessages.Add(message);
        db.Notifications.AddRange(notifications);
        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync(cancellationToken);
    }
}
