using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;
using Microsoft.EntityFrameworkCore;

namespace DigitMak.Portal.Api.Infrastructure.Persistence.Repositories;

public sealed class MeetingRepository(PortalDbContext db) : IMeetingRepository
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

    public Task<bool> OwnsTicketAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken) =>
        db.Tickets.AnyAsync(ticket => ticket.Id == ticketId && ticket.CreatedByUserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Meeting>> ListRequestedByAsync(Guid userId, CancellationToken cancellationToken) =>
        await db
            .Meetings.Where(meeting => meeting.RequestedByUserId == userId)
            .OrderByDescending(meeting => meeting.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Meeting>> ListCreatedByAsync(Guid userId, CancellationToken cancellationToken) =>
        await db
            .Meetings.Where(meeting => meeting.CreatedByUserId == userId && meeting.RequestedByUserId != userId)
            .OrderByDescending(meeting => meeting.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        Meeting meeting,
        Notification notification,
        AuditLog auditLog,
        CancellationToken cancellationToken
    )
    {
        db.Meetings.Add(meeting);
        db.Notifications.Add(notification);
        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync(cancellationToken);
    }
}
