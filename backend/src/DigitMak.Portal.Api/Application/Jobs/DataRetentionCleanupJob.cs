using Microsoft.EntityFrameworkCore;
using Quartz;
using DigitMak.Portal.Api.Infrastructure.Persistence;

namespace DigitMak.Portal.Api.Application.Jobs;

/// <summary>
/// Housekeeping that doesn't need to run often: drops refresh tokens that expired more than
/// 30 days ago, and prunes notifications past the configured DataRetentionDays system setting
/// (defaults to 730 days / 2 years). Scheduled once a night.
/// </summary>
[DisallowConcurrentExecution]
public sealed class DataRetentionCleanupJob(PortalDbContext db) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var now = DateTimeOffset.UtcNow;

        var stale = await db.RefreshTokens.Where(x => x.ExpiresAt < now.AddDays(-30)).ToListAsync(ct);
        db.RefreshTokens.RemoveRange(stale);

        var retentionDays = int.TryParse(
            await db
                .SystemSettings.Where(x => x.Key == "DataRetentionDays")
                .Select(x => x.Value)
                .SingleOrDefaultAsync(ct),
            out var days
        )
            ? days
            : 730;
        var oldNotifications = await db
            .Notifications.Where(x => x.CreatedAt < now.AddDays(-retentionDays))
            .ToListAsync(ct);
        db.Notifications.RemoveRange(oldNotifications);

        await db.SaveChangesAsync(ct);
    }
}
