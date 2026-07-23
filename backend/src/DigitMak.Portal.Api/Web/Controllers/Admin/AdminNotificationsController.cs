using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using static DigitMak.Portal.Api.Web.Controllers.Admin.AdminSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Admin;

[ApiController]
[Route("api/admin/notifications")]
[Authorize(Policy = "Admin")]
public sealed class AdminNotificationsController(PortalDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<object> Get(string? status, CancellationToken ct) =>
        await db
            .Notifications.Where(item => status == null || item.Status == status)
            .OrderByDescending(item => item.CreatedAt)
            .Take(200)
            .Select(item => new
            {
                item.Id,
                item.RecipientUserId,
                item.RecipientEmail,
                item.Type,
                item.Language,
                item.Subject,
                item.Status,
                item.AttemptCount,
                item.NextAttemptAt,
                item.LastError,
                item.SentAt,
                item.CreatedAt,
            })
            .ToListAsync(ct);

    [HttpPost("{id:guid}/retry")]
    public async Task<IResult> Retry(Guid id, CancellationToken ct)
    {
        var principal = User;
        var item = await db.Notifications.FindAsync([id], ct);
        if (item is null)
            return Results.NotFound();
        item.Status = "Queued";
        item.NextAttemptAt = DateTimeOffset.UtcNow;
        item.LastError = null;
        item.AttemptCount = 0;
        db.AuditLogs.Add(Audit(principal, "NotificationRetryQueued", nameof(Notification), item.Id));
        await db.SaveChangesAsync(ct);
        return Results.Accepted();
    }
}
