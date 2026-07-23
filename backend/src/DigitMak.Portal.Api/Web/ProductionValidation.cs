namespace DigitMak.Portal.Api.Web;

public static class ProductionValidation
{
    public static void Ensure(IConfiguration config, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
            return;
        var required = new[]
        {
            "ConnectionStrings:Portal",
            "Jwt:Key",
            "BREVO_SMTP_HOST",
            "BREVO_SMTP_USERNAME",
            "BREVO_SMTP_PASSWORD",
            "BREVO_FROM_EMAIL",
            "APP_PUBLIC_URL",
            "ADMIN_BOOTSTRAP_EMAIL",
            "ADMIN_BOOTSTRAP_PASSWORD",
            "UPLOADS_ROOT",
            "CLAMAV_HOST",
        };
        var missing = required
            .Where(k =>
                string.IsNullOrWhiteSpace(config[k])
                || config[k]!.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
            )
            .ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException(
                "Missing production configuration: " + string.Join(", ", missing)
            );
        if (config["Jwt:Key"]!.Length < 64)
            throw new InvalidOperationException(
                "Jwt:Key must contain at least 64 characters in production."
            );
    }
}
