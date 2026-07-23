using Microsoft.EntityFrameworkCore;
using Quartz;
using DigitMak.Portal.Api.Infrastructure.Persistence;

namespace DigitMak.Portal.Api.Application.Jobs;

/// <summary>
/// Warns subscribers whose annual subscription is about to expire, and hard-expires the
/// ones whose ExpiresAt has already passed. Runs every 15 minutes — frequent enough that a
/// subscription is never more than 15 minutes late to flip to "Expired" and lose ticket access.
/// </summary>
[DisallowConcurrentExecution]
public sealed class SubscriptionMaintenanceJob(PortalDbContext db) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var now = DateTimeOffset.UtcNow;

        var expiring = await db
            .Subscriptions.Where(x =>
                x.Status == "Active" && x.ExpiresAt > now && x.ExpiresAt <= now.AddDays(30)
            )
            .ToListAsync(ct);
        foreach (var item in expiring)
            if (
                !await db.Notifications.AnyAsync(
                    x =>
                        x.RecipientUserId == item.UserId
                        && x.Type == "SubscriptionExpiringSoon"
                        && x.CreatedAt > now.AddDays(-7),
                    ct
                )
            )
                db.Notifications.Add(
                    new Notification
                    {
                        RecipientUserId = item.UserId,
                        Type = "SubscriptionExpiringSoon",
                        Subject = "Вашата претплата истекува наскоро",
                        Body = $"<p>Вашата претплата истекува на {item.ExpiresAt:yyyy-MM-dd}.</p>",
                        ActionUrl = "/portal?tab=organization",
                    }
                );

        var expired = await db
            .Subscriptions.Where(x => x.Status == "Active" && x.ExpiresAt <= now)
            .ToListAsync(ct);
        foreach (var item in expired)
        {
            item.Status = "Expired";
            db.Notifications.Add(
                new Notification
                {
                    RecipientUserId = item.UserId,
                    Type = "SubscriptionExpired",
                    Subject = "Вашата претплата истече",
                    Body = "<p>Вашата претплата истече. Обратете се до администратор за продолжување.</p>",
                    ActionUrl = "/portal?tab=organization",
                }
            );
            db.AuditLogs.Add(
                new AuditLog
                {
                    Action = "SubscriptionExpired",
                    EntityType = nameof(Subscription),
                    EntityId = item.Id.ToString(),
                }
            );
        }
        await db.SaveChangesAsync(ct);
    }
}
