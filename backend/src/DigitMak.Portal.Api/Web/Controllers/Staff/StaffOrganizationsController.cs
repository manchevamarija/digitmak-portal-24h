using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;

namespace DigitMak.Portal.Api.Web.Controllers.Staff;

[ApiController]
[Route("api/staff/organizations")]
[Authorize(Policy = "Staff")]
public sealed class StaffOrganizationsController(PortalDbContext db) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IResult> Get(Guid id, CancellationToken ct)
    {
        var principal = User;
        var userId = principal.UserId();
        var allowed =
            principal.IsInRole("Admin")
            || principal.IsInRole("HelpDeskAgent")
            || await db.Tickets.AnyAsync(
                x =>
                    x.OrganizationId == id
                    && (x.AssignedAgentId == userId || x.AssignedExpertId == userId),
                ct
            )
            || await db.ContactRequests.AnyAsync(
                x => x.LinkedOrganizationId == id && x.AssignedTo == userId,
                ct
            );
        if (!allowed)
            return Results.Forbid();
        var organization = await db.Organizations.FindAsync([id], ct);
        if (organization is null)
            return Results.NotFound();
        var members = await db
            .OrganizationMembers.Where(x => x.OrganizationId == id && x.MemberStatus == "Active")
            .Join(
                db.Users,
                member => member.UserId,
                user => user.Id,
                (member, user) =>
                    new
                    {
                        user.Id,
                        user.FirstName,
                        user.LastName,
                        user.Email,
                        user.PhoneNumber,
                        member.IsPrimaryContact,
                    }
            )
            .ToListAsync(ct);
        return Results.Ok(new { organization, members });
    }
}
