using DigitMak.Portal.Api.Application;
using Microsoft.EntityFrameworkCore;
using Quartz;
using DigitMak.Portal.Api.Infrastructure.Persistence;

namespace DigitMak.Portal.Api.Application.Jobs;

/// <summary>
/// Sends queued Notification rows through IEmailSender, with exponential backoff on failure.
/// Runs every minute — this is the time-sensitive half of what used to be one big
/// NotificationWorker BackgroundService loop.
/// </summary>
[DisallowConcurrentExecution]
public sealed class NotificationDispatchJob(
    PortalDbContext db,
    IEmailSender email,
    ILogger<NotificationDispatchJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var now = DateTimeOffset.UtcNow;
        var queued = await db
            .Notifications.Where(x =>
                x.Status == "Queued" && (x.NextAttemptAt == null || x.NextAttemptAt <= now)
            )
            .Take(20)
            .ToListAsync(ct);
        foreach (var notification in queued)
        {
            var userData = notification.RecipientUserId is null
                ? null
                : await db
                    .Users.Where(x => x.Id == notification.RecipientUserId.Value)
                    .Select(x => new { x.Email, x.PreferredLanguage })
                    .SingleOrDefaultAsync(ct);
            var address = notification.RecipientEmail ?? userData?.Email;
            if (address is null)
            {
                notification.Status = "Failed";
                notification.LastError = "Recipient not found";
                continue;
            }
            try
            {
                var rendered = EmailTemplates.Render(
                    notification.Type,
                    notification.Language ?? userData?.PreferredLanguage,
                    notification.Subject,
                    notification.Body
                );
                await email.SendAsync(address, rendered.Subject, rendered.Body, ct);
                notification.Status = "Sent";
                notification.SentAt = DateTimeOffset.UtcNow;
                notification.LastError = null;
            }
            catch (Exception ex)
            {
                notification.AttemptCount++;
                notification.LastError = ex.Message[..Math.Min(ex.Message.Length, 500)];
                if (notification.AttemptCount >= 5)
                    notification.Status = "Failed";
                else
                    notification.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(
                        Math.Pow(2, notification.AttemptCount)
                    );
                logger.LogWarning(
                    ex,
                    "Email delivery deferred (attempt {Attempt})",
                    notification.AttemptCount
                );
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
