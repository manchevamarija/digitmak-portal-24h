using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using static DigitMak.Portal.Api.Web.Controllers.Client.ClientSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Client;

[ApiController]
[Route("api/account-change-requests")]
[Authorize]
public sealed class AccountChangeRequestsController(PortalDbContext db) : ControllerBase
{
    [HttpGet("my")]
    public async Task<IResult> Mine(CancellationToken ct)
    {
        var principal = User;
        return Results.Ok(
            await db
                .AccountChangeRequests.Where(x => x.UserId == principal.UserId())
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(ct)
        );
    }

    [HttpPost]
    public async Task<IResult> Create(AccountChangeRequestDto request, CancellationToken ct)
    {
        var principal = User;
        if (request.RequestType is not ("Organization" or "Subscription"))
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["requestType"] = ["Choose Organization or Subscription."],
                }
            );
        if (request.Details.Trim().Length < 10)
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["details"] = ["Enter at least 10 characters."] }
            );
        var user = await db.Users.FindAsync([principal.UserId()], ct);
        if (user?.OrganizationId is not Guid organizationId)
            return Results.Conflict(new { message = "An organization is required." });
        if (
            await db.AccountChangeRequests.AnyAsync(
                x =>
                    x.UserId == user.Id
                    && x.RequestType == request.RequestType
                    && x.Status == "Pending",
                ct
            )
        )
            return Results.Conflict(new { message = "A request of this type is already pending." });
        var item = new AccountChangeRequest
        {
            UserId = user.Id,
            OrganizationId = organizationId,
            RequestType = request.RequestType,
            Details = request.Details.Trim(),
        };
        db.AccountChangeRequests.Add(item);
        await NotifyAdminsAsync(
            db,
            "AccountChangeRequested",
            $"Барање за промена: {user.Email}",
            $"<p>{user.FirstName} {user.LastName} ({user.Email}) поднесе барање за промена на {(request.RequestType == "Organization" ? "организација" : "претплата")}.</p>",
            "/admin?tab=changes",
            ct
        );
        db.AuditLogs.Add(
            Audit(principal, "AccountChangeRequested", nameof(AccountChangeRequest), item.Id)
        );
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/account-change-requests/{item.Id}", item);
    }

    [HttpPost("{id:guid}/apply-organization")]
    public async Task<IResult> ApplyOrganization(
        Guid id,
        ApplyOrganizationChangeRequest request,
        CancellationToken ct
    )
    {
        var principal = User;
        var item = await db.AccountChangeRequests.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == principal.UserId(),
            ct
        );
        if (item is null)
            return Results.NotFound();
        if (item.RequestType != "Organization" || item.Status != "Accepted")
            return Results.Conflict(
                new { message = "This organization change is not ready to apply." }
            );
        var user = await db.Users.FindAsync([principal.UserId()], ct);
        if (user is null)
            return Results.NotFound();
        if (user.OrganizationId == request.OrganizationId)
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["organizationId"] = ["Choose a different organization."],
                }
            );
        if (
            !await db.Organizations.AnyAsync(
                x => x.Id == request.OrganizationId && x.Status == "Approved",
                ct
            )
        )
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["organizationId"] = ["Choose an approved organization."],
                }
            );

        var oldOrganizationId = user.OrganizationId;
        var oldMemberships = await db
            .OrganizationMembers.Where(x => x.UserId == user.Id && x.MemberStatus == "Active")
            .ToListAsync(ct);
        foreach (var membership in oldMemberships)
            membership.MemberStatus = "Transferred";

        var targetMembership = await db.OrganizationMembers.SingleOrDefaultAsync(
            x => x.UserId == user.Id && x.OrganizationId == request.OrganizationId,
            ct
        );
        if (targetMembership is null)
        {
            targetMembership = new OrganizationMember
            {
                UserId = user.Id,
                OrganizationId = request.OrganizationId,
            };
            db.OrganizationMembers.Add(targetMembership);
        }
        targetMembership.MemberStatus = "Active";
        user.OrganizationId = request.OrganizationId;
        item.Status = "Applied";
        item.DecidedAt = DateTimeOffset.UtcNow;
        db.AuditLogs.Add(
            Audit(
                principal,
                "OrganizationChangeApplied",
                nameof(AccountChangeRequest),
                item.Id,
                JsonSerializer.Serialize(
                    new
                    {
                        OldOrganizationId = oldOrganizationId,
                        NewOrganizationId = request.OrganizationId,
                    }
                )
            )
        );
        await db.SaveChangesAsync(ct);
        return Results.Ok(item);
    }
}
