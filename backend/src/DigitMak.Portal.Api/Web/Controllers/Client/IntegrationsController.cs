using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using static DigitMak.Portal.Api.Web.Controllers.Client.ClientSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Client;

[ApiController]
[Route("api/integrations")]
[Authorize]
public sealed class IntegrationsController(IConfiguration config, UserManager<AppUser> users)
    : ControllerBase
{
    [HttpGet("status")]
    public IResult Status() =>
        Results.Ok(
            new
            {
                moodle = new
                {
                    configured = !string.IsNullOrWhiteSpace(config["MOODLE_BASE_URL"]),
                    mode = config["MOODLE_SSO_MODE"]
                        ?? (
                            string.IsNullOrWhiteSpace(config["MOODLE_SSO_SHARED_SECRET"])
                                ? "ExternalLink"
                                : "SignedLaunch"
                        ),
                },
                calendars = new
                {
                    ics = new { configured = true, exportUrl = "/api/meetings/calendar.ics" },
                    google = new { configured = CalendarProviderConfigured(config, "GOOGLE_CALENDAR") },
                    microsoft = new
                    {
                        configured = CalendarProviderConfigured(config, "MICROSOFT_CALENDAR"),
                    },
                },
            }
        );

    [HttpGet("calendars/{provider}/authorize")]
    public IResult AuthorizeCalendar(string provider)
    {
        var principal = User;
        var normalized = provider.ToLowerInvariant();
        var prefix = normalized switch
        {
            "google" => "GOOGLE_CALENDAR",
            "microsoft" => "MICROSOFT_CALENDAR",
            _ => null,
        };
        if (prefix is null)
            return Results.BadRequest(
                new { message = "Supported calendar providers are google and microsoft." }
            );
        if (!CalendarProviderConfigured(config, prefix))
            return Results.Conflict(
                new { message = $"{provider} calendar credentials are not configured." }
            );
        var clientId = config[$"{prefix}_CLIENT_ID"]!;
        var redirect = config[$"{prefix}_REDIRECT_URI"]!;
        var statePayload =
            $"{principal.UserId()}|{normalized}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var state = SignIntegrationState(statePayload, config["Jwt:Key"]!);
        var url =
            normalized == "google"
                ? $"https://accounts.google.com/o/oauth2/v2/auth?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirect)}&response_type=code&access_type=offline&prompt=consent&scope={Uri.EscapeDataString("openid email https://www.googleapis.com/auth/calendar.events")}&state={Uri.EscapeDataString(state)}"
                : $"https://login.microsoftonline.com/{Uri.EscapeDataString(config[$"{prefix}_TENANT_ID"] ?? "common")}/oauth2/v2.0/authorize?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirect)}&response_type=code&response_mode=query&scope={Uri.EscapeDataString("openid offline_access User.Read Calendars.ReadWrite")}&state={Uri.EscapeDataString(state)}";
        return Results.Ok(
            new
            {
                provider = normalized,
                authorizationUrl = url,
                callbackUrl = redirect,
                stateExpiresInSeconds = 600,
            }
        );
    }

    [HttpGet("moodle/launch")]
    public async Task<IResult> MoodleLaunch()
    {
        var principal = User;
        var baseUrl = config["MOODLE_BASE_URL"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return Results.NotFound(new { message = "Moodle is not configured." });
        var user = await users.GetUserAsync(principal);
        if (user is null)
            return Results.Unauthorized();
        var configuredMode = config["MOODLE_SSO_MODE"]?.ToUpperInvariant();
        if (configuredMode is "OIDC" or "SAML")
        {
            var entryPoint = config["MOODLE_SSO_ENTRY_URL"];
            if (string.IsNullOrWhiteSpace(entryPoint))
                return Results.Conflict(
                    new { message = $"Moodle {configuredMode} mode requires MOODLE_SSO_ENTRY_URL." }
                );
            return Results.Ok(new { url = entryPoint, mode = configuredMode });
        }
        var secret = config["MOODLE_SSO_SHARED_SECRET"];
        if (string.IsNullOrWhiteSpace(secret))
            return Results.Ok(new { url = baseUrl, mode = "ExternalLink" });
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var payload = $"{user.Id}|{user.Email}|{user.PreferredLanguage}|{timestamp}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = Convert
            .ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
        var separator = baseUrl.Contains('?') ? "&" : "?";
        var url =
            $"{baseUrl}{separator}digitmak_user={Uri.EscapeDataString(user.Id.ToString())}&email={Uri.EscapeDataString(user.Email ?? "")}&name={Uri.EscapeDataString($"{user.FirstName} {user.LastName}")}&lang={Uri.EscapeDataString(user.PreferredLanguage)}&ts={timestamp}&signature={signature}";
        return Results.Ok(new { url, mode = "SignedLaunch" });
    }
}
