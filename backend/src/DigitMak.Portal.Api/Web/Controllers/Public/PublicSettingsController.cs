using Microsoft.AspNetCore.Mvc;

namespace DigitMak.Portal.Api.Web.Controllers.Public;

[ApiController]
[Route("api/public/settings")]
public sealed class PublicSettingsController(IConfiguration config) : ControllerBase
{
    [HttpGet("locales")]
    public IResult GetLocales() => Results.Ok(new[] { "mk", "en", "sq" });

    [HttpGet("integrations")]
    public IResult GetIntegrations() =>
        Results.Ok(
            new
            {
                moodle = new
                {
                    configured = !string.IsNullOrWhiteSpace(config["MOODLE_BASE_URL"]),
                    mode = string.IsNullOrWhiteSpace(config["MOODLE_SSO_SHARED_SECRET"])
                        ? "ExternalLink"
                        : "SignedLaunch",
                    requiresProviderAgreement = true,
                },
                calendar = new
                {
                    icsExport = true,
                    googleImport = true,
                    microsoftImport = true,
                    nativeSync = false,
                    nativeSyncPhase = "V2",
                },
            }
        );
}
