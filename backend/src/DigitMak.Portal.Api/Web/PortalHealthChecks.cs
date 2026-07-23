using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using DigitMak.Portal.Api.Infrastructure.Persistence;

namespace DigitMak.Portal.Api.Web;

public static class PortalHealthChecks
{
    public static IServiceCollection AddPortalHealthChecks(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck(
                "application",
                () => HealthCheckResult.Healthy("DigitMak API is running."),
                ["live"]
            )
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready", "database"])
            .AddCheck<StorageHealthCheck>("storage", tags: ["ready", "storage"])
            .AddCheck<SmtpHealthCheck>("smtp", tags: ["ready", "external"])
            .AddCheck<ClamAvHealthCheck>("antivirus", tags: ["ready", "external"]);
        return services;
    }
}

public sealed class DatabaseHealthCheck(PortalDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default
    )
    {
        try
        {
            if (!await db.Database.CanConnectAsync(ct))
                return HealthCheckResult.Unhealthy("Database connection failed.");
            var provider = db.Database.ProviderName ?? "unknown";
            return HealthCheckResult.Healthy(
                "Database is reachable.",
                new Dictionary<string, object> { ["provider"] = provider }
            );
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database readiness check failed.", ex);
        }
    }
}

public sealed class StorageHealthCheck(IConfiguration config, IHostEnvironment environment)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default
    )
    {
        var root = Path.GetFullPath(
            config["UPLOADS_ROOT"] ?? Path.Combine(AppContext.BaseDirectory, "uploads")
        );
        try
        {
            Directory.CreateDirectory(root);
            var probe = Path.Combine(root, $".health-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(probe, "digitmak-storage-health", ct);
            File.Delete(probe);
            return HealthCheckResult.Healthy(
                "Upload storage is writable.",
                new Dictionary<string, object>
                {
                    ["environment"] = environment.EnvironmentName,
                    ["root"] = root,
                }
            );
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Upload storage is not writable.",
                ex,
                new Dictionary<string, object> { ["root"] = root }
            );
        }
    }
}

public sealed class SmtpHealthCheck(IConfiguration config, IHostEnvironment environment)
    : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default
    ) =>
        TcpCheck.CheckAsync(
            "SMTP",
            config["BREVO_SMTP_HOST"],
            config.GetValue<int?>("BREVO_SMTP_PORT") ?? 587,
            environment.IsProduction(),
            ct
        );
}

public sealed class ClamAvHealthCheck(IConfiguration config, IHostEnvironment environment)
    : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default
    ) =>
        TcpCheck.CheckAsync(
            "ClamAV",
            config["CLAMAV_HOST"],
            config.GetValue<int?>("CLAMAV_PORT") ?? 3310,
            environment.IsProduction(),
            ct
        );
}

internal static class TcpCheck
{
    public static async Task<HealthCheckResult> CheckAsync(
        string service,
        string? host,
        int port,
        bool required,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(host))
            return required
                ? HealthCheckResult.Unhealthy($"{service} is not configured.")
                : HealthCheckResult.Degraded(
                    $"{service} is optional and not configured in this environment."
                );
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, timeout.Token);
            return HealthCheckResult.Healthy(
                $"{service} endpoint is reachable.",
                new Dictionary<string, object> { ["host"] = host, ["port"] = port }
            );
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"{service} endpoint is not reachable.",
                ex,
                new Dictionary<string, object> { ["host"] = host, ["port"] = port }
            );
        }
    }
}
