using System.Net;
using System.Net.Mail;

namespace DigitMak.Portal.Api.Application;

public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken ct);
}

public class BrevoEmailSender(IConfiguration config, ILogger<BrevoEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken ct)
    {
        var host = config["BREVO_SMTP_HOST"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogWarning("SMTP not configured; email retained for retry: {Subject}", subject);
            throw new InvalidOperationException("SMTP is not configured");
        }
        using var client = new SmtpClient(
            host,
            int.TryParse(config["BREVO_SMTP_PORT"], out var p) ? p : 587
        )
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(
                config["BREVO_SMTP_USERNAME"],
                config["BREVO_SMTP_PASSWORD"]
            ),
        };
        using var message = new MailMessage(
            new MailAddress(
                config["BREVO_FROM_EMAIL"] ?? "noreply@digitmak.mk",
                config["BREVO_FROM_NAME"] ?? "DigitMak"
            ),
            new MailAddress(recipient)
        )
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
        };
        await client.SendMailAsync(message, ct);
    }
}
