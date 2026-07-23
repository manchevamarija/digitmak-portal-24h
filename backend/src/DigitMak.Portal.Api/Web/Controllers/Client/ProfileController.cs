using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using static DigitMak.Portal.Api.Web.Controllers.Client.ClientSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Client;

[ApiController]
[Route("api/profile")]
[Authorize]
public sealed class ProfileController(PortalDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<object> Get(CancellationToken ct)
    {
        var principal = User;
        return await db
            .Users.Where(x => x.Id == principal.UserId())
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.FirstName,
                x.LastName,
                x.PhoneNumber,
                x.PreferredLanguage,
                x.OrganizationId,
            })
            .SingleAsync(ct);
    }

    [HttpPatch]
    public async Task<IResult> Update(ProfileRequest request)
    {
        var principal = User;
        var user = await db.Users.FindAsync(principal.UserId());
        if (user is null)
            return Results.NotFound();
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.Phone;
        user.PreferredLanguage = request.PreferredLanguage;
        db.AuditLogs.Add(Audit(principal, "ProfileUpdated", nameof(AppUser), user.Id));
        await db.SaveChangesAsync();
        return Results.Ok(
            new
            {
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.PhoneNumber,
                user.PreferredLanguage,
            }
        );
    }

    [HttpGet("consents")]
    public async Task<IResult> Consents(CancellationToken ct)
    {
        var principal = User;
        var accepted = await db
            .AuditLogs.Where(x =>
                x.ActorUserId == principal.UserId() && x.Action == "LegalConsentAccepted"
            )
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.CreatedAt, x.MetadataJson })
            .ToListAsync(ct);
        return Results.Ok(
            new
            {
                current = new
                {
                    termsVersion = LegalDocumentVersions.Terms,
                    privacyVersion = LegalDocumentVersions.Privacy,
                },
                accepted,
            }
        );
    }
}
