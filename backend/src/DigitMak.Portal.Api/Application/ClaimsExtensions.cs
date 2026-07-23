using System.Security.Claims;

namespace DigitMak.Portal.Api.Application;

/// <summary>
/// Lives in Application (not Infrastructure) on purpose: Application-layer services such as
/// TicketService call <c>principal.UserId()</c> directly, so the extension has to be visible
/// from a project that Application itself references — it has zero framework dependency
/// beyond System.Security.Claims, so there is no Onion-layering cost to keeping it here.
/// </summary>
public static class ClaimsExtensions
{
    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
