using Quartz;

namespace DigitMak.Portal.Api.Application.Jobs;

public static class QuartzConfiguration
{
    public static IServiceCollection AddPortalScheduledJobs(
        this IServiceCollection services,
        IHostEnvironment environment
    )
    {
        // Keep the "Testing" environment (used by the integration test host) fully
        // deterministic — no background timer firing mid-test and mutating rows a test
        // is asserting against.
        if (environment.IsEnvironment("Testing"))
            return services;

        services.AddQuartz(q =>
        {

            var dispatchKey = new JobKey(nameof(NotificationDispatchJob));
            q.AddJob<NotificationDispatchJob>(o => o.WithIdentity(dispatchKey));
            q.AddTrigger(t =>
                t.ForJob(dispatchKey)
                    .WithIdentity($"{nameof(NotificationDispatchJob)}-trigger")
                    .WithSimpleSchedule(s => s.WithIntervalInMinutes(1).RepeatForever())
                    .StartNow()
            );

            var subscriptionKey = new JobKey(nameof(SubscriptionMaintenanceJob));
            q.AddJob<SubscriptionMaintenanceJob>(o => o.WithIdentity(subscriptionKey));
            q.AddTrigger(t =>
                t.ForJob(subscriptionKey)
                    .WithIdentity($"{nameof(SubscriptionMaintenanceJob)}-trigger")
                    .WithSimpleSchedule(s => s.WithIntervalInMinutes(15).RepeatForever())
                    .StartNow()
            );

            var retentionKey = new JobKey(nameof(DataRetentionCleanupJob));
            q.AddJob<DataRetentionCleanupJob>(o => o.WithIdentity(retentionKey));
            q.AddTrigger(t =>
                t.ForJob(retentionKey)
                    .WithIdentity($"{nameof(DataRetentionCleanupJob)}-trigger")
                    // Every night at 02:30 — low-traffic window, cron: sec min hour day month day-of-week
                    .WithCronSchedule("0 30 2 * * ?")
            );

            var escalationKey = new JobKey(nameof(TicketEscalationJob));
            q.AddJob<TicketEscalationJob>(o => o.WithIdentity(escalationKey));
            q.AddTrigger(t =>
                t.ForJob(escalationKey)
                    .WithIdentity($"{nameof(TicketEscalationJob)}-trigger")
                    .WithSimpleSchedule(s => s.WithIntervalInMinutes(30).RepeatForever())
                    .StartNow()
            );
        });
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
        return services;
    }
}
