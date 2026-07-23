using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;
using Microsoft.EntityFrameworkCore;

namespace DigitMak.Portal.Api.Infrastructure.Persistence.Repositories;

public sealed class ContactRequestRepository(PortalDbContext db) : IContactRequestRepository
{
    public async Task<IReadOnlyList<Guid>> GetAdminUserIdsAsync(CancellationToken cancellationToken) =>
        await db
            .UserRoles.Join(
                db.Roles.Where(role => role.Name == "Admin"),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, _) => userRole.UserId
            )
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        ContactRequest request,
        IReadOnlyList<Notification> notifications,
        AuditLog auditLog,
        CancellationToken cancellationToken
    )
    {
        db.ContactRequests.Add(request);
        db.Notifications.AddRange(notifications);
        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync(cancellationToken);
    }
}
