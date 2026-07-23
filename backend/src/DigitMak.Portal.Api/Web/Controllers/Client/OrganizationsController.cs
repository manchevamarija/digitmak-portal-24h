using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using static DigitMak.Portal.Api.Web.Controllers.Client.ClientSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Client;

[ApiController]
[Route("api/organizations")]
[Authorize]
public sealed class OrganizationsController(PortalDbContext db, UserManager<AppUser> users)
    : ControllerBase
{
    [HttpPost]
    public async Task<IResult> Create(OrganizationRequest request)
    {
        var principal = User;
        var user = await users.GetUserAsync(principal);
        if (user!.OrganizationId is not null)
            return Results.Json(
                new
                {
                    code = "ORGANIZATION_ALREADY_ASSIGNED",
                    message = "User already belongs to an organization.",
                },
                statusCode: StatusCodes.Status409Conflict
            );
        var item = new Organization
        {
            Name = request.Name,
            Type = request.Type,
            Sector = request.Sector,
            Municipality = request.Municipality,
            Region = request.Region,
            Website = request.Website,
            EmployeeCount = request.EmployeeCount,
            CreatedByUserId = user.Id,
        };
        db.Add(item);
        user.OrganizationId = item.Id;
        db.OrganizationMembers.Add(
            new OrganizationMember
            {
                OrganizationId = item.Id,
                UserId = user.Id,
                IsPrimaryContact = true,
            }
        );
        db.Notifications.Add(
            new Notification
            {
                RecipientUserId = user.Id,
                Type = "OrganizationSubmitted",
                Subject = "Организацијата е поднесена за одобрување",
                Body = "<p>Вашата организација е поднесена и чека одобрување од администратор.</p>",
                ActionUrl = "/portal?tab=organization",
            }
        );
        await NotifyAdminsAsync(
            db,
            "OrganizationSubmittedForApproval",
            $"Нова организација чека одобрување: {item.Name}",
            $"<p>{user.FirstName} {user.LastName} ({user.Email}) поднесе организација „{item.Name}“ за одобрување.</p>",
            $"/admin?tab=organizations&org={item.Id}"
        );
        db.AuditLogs.Add(Audit(principal, "OrganizationCreated", nameof(Organization), item.Id));
        await db.SaveChangesAsync();
        return Results.Created("/api/organizations/my", item);
    }

    [HttpPost("{id:guid}/join")]
    public async Task<IResult> Join(Guid id)
    {
        var principal = User;
        var user = await db.Users.FindAsync(principal.UserId());
        if (user?.OrganizationId is not null)
            return Results.Json(
                new
                {
                    code = "ORGANIZATION_ALREADY_ASSIGNED",
                    message = "User already belongs to an organization.",
                },
                statusCode: StatusCodes.Status409Conflict
            );
        if (!await db.Organizations.AnyAsync(x => x.Id == id && x.Status == "Approved"))
            return Results.Json(
                new
                {
                    code = "ORGANIZATION_NOT_AVAILABLE",
                    message = "The selected organization is not available for joining.",
                },
                statusCode: StatusCodes.Status404NotFound
            );
        if (
            await db.OrganizationMembers.AnyAsync(x =>
                x.OrganizationId == id && x.UserId == principal.UserId()
            )
        )
            return Results.Json(
                new
                {
                    code = "ORGANIZATION_MEMBERSHIP_EXISTS",
                    message = "A membership request already exists for this organization.",
                },
                statusCode: StatusCodes.Status409Conflict
            );
        var member = new OrganizationMember
        {
            OrganizationId = id,
            UserId = principal.UserId(),
            MemberStatus = "Pending",
        };
        db.Add(member);
        var organizationName = await db
            .Organizations.Where(x => x.Id == id)
            .Select(x => x.Name)
            .SingleOrDefaultAsync();
        await NotifyAdminsAsync(
            db,
            "OrganizationMembershipRequested",
            $"Барање за членство: {user.Email}",
            $"<p>{user.FirstName} {user.LastName} ({user.Email}) сака да се приклучи кон „{organizationName}“.</p>",
            $"/admin?tab=organizations&org={id}"
        );
        db.AuditLogs.Add(
            Audit(principal, "OrganizationJoinRequested", nameof(OrganizationMember), member.Id)
        );
        await db.SaveChangesAsync();
        return Results.Accepted($"/api/organizations/{id}/membership", member);
    }

    [HttpGet("available")]
    public async Task<IResult> Available(CancellationToken ct) =>
        Results.Ok(
            await db
                .Organizations.Where(x => x.Status == "Approved")
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Type,
                    x.Region,
                })
                .OrderBy(x => x.Name)
                .ToListAsync(ct)
        );

    [HttpGet("my")]
    public async Task<IResult> Mine(CancellationToken ct)
    {
        var principal = User;
        var organizationId = await db
            .Users.Where(x => x.Id == principal.UserId())
            .Select(x => x.OrganizationId)
            .SingleOrDefaultAsync(ct);
        return organizationId is not null
            && await db.Organizations.FindAsync([organizationId.Value], ct) is { } item
            ? Results.Ok(item)
            : Results.NotFound();
    }

    [HttpPatch("my")]
    public async Task<IResult> UpdateMine(OrganizationRequest request)
    {
        var principal = User;
        var user = await db.Users.FindAsync(principal.UserId());
        if (user?.OrganizationId is null)
            return Results.NotFound();
        var item = await db.Organizations.FindAsync(user.OrganizationId.Value);
        if (item is null)
            return Results.NotFound();
        item.Name = request.Name;
        item.Type = request.Type;
        item.Sector = request.Sector;
        item.Municipality = request.Municipality;
        item.Region = request.Region;
        item.Website = request.Website;
        item.EmployeeCount = request.EmployeeCount;
        item.Status = "PendingApproval";
        db.AuditLogs.Add(Audit(principal, "OrganizationUpdated", nameof(Organization), item.Id));
        await db.SaveChangesAsync();
        return Results.Ok(item);
    }
}
