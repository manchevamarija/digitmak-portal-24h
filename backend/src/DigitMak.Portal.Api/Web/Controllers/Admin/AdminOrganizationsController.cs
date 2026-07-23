using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using static DigitMak.Portal.Api.Web.Controllers.Admin.AdminSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "Admin")]
public sealed class AdminOrganizationsController(PortalDbContext db) : ControllerBase
{
    [HttpGet("organizations")]
    public async Task<IReadOnlyList<Organization>> Get(int? page, int? pageSize, CancellationToken ct) =>
        await db
            .Organizations.OrderByDescending(x => x.CreatedAt)
            .Skip(Offset(page, pageSize))
            .Take(Size(pageSize))
            .ToListAsync(ct);

    [HttpGet("organizations/{id:guid}")]
    public async Task<IResult> GetOne(Guid id, CancellationToken ct) =>
        await db.Organizations.FindAsync([id], ct) is { } item
            ? Results.Ok(
                new
                {
                    organization = item,
                    members = await db
                        .OrganizationMembers.Where(x => x.OrganizationId == id)
                        .Join(
                            db.Users,
                            x => x.UserId,
                            x => x.Id,
                            (member, user) =>
                                new
                                {
                                    member.Id,
                                    member.UserId,
                                    user.Email,
                                    user.FirstName,
                                    user.LastName,
                                    member.MemberStatus,
                                    member.IsPrimaryContact,
                                }
                        )
                        .ToListAsync(ct),
                }
            )
            : Results.NotFound();

    [HttpPost("organizations/{id:guid}/{operation}")]
    public async Task<IResult> ChangeStatus(Guid id, string operation)
    {
        var principal = User;
        var item = await db.Organizations.FindAsync(id);
        if (item is null)
            return Results.NotFound();
        var next = operation.ToLowerInvariant() switch
        {
            "approve" => "Approved",
            "reject" => "Rejected",
            "suspend" => "Suspended",
            "reactivate" => "Approved",
            _ => null,
        };
        if (next is null)
            return Results.BadRequest();
        if (
            operation.Equals("reactivate", StringComparison.OrdinalIgnoreCase)
            && item.Status != "Suspended"
        )
            return Results.Conflict(
                new { message = "Only a suspended organization can be reactivated." }
            );
        var old = item.Status;
        item.Status = next;
        if (next == "Approved")
        {
            item.ApprovedBy = principal.UserId();
            item.ApprovedAt = DateTimeOffset.UtcNow;
        }
        var recipients = await db
            .OrganizationMembers.Where(x => x.OrganizationId == id)
            .Select(x => x.UserId)
            .ToListAsync();
        if (!recipients.Contains(item.CreatedByUserId))
            recipients.Add(item.CreatedByUserId);
        var (orgSubject, orgBody) = next switch
        {
            "Approved" => (
                "Администраторот го одобри вашето барање",
                $"<p>Организацијата <strong>{item.Name}</strong> е одобрена. Можете да продолжите со претплата.</p>"
            ),
            "Rejected" => (
                "Администраторот го одби вашето барање",
                $"<p>Барањето за организацијата <strong>{item.Name}</strong> е одбиено.</p>"
            ),
            "Suspended" => (
                "Организацијата е суспендирана",
                $"<p>Организацијата <strong>{item.Name}</strong> е привремено суспендирана.</p>"
            ),
            _ => (
                "Статус на организација",
                $"<p>Организацијата <strong>{item.Name}</strong> е {next.ToLowerInvariant()}.</p>"
            ),
        };
        foreach (var recipient in recipients.Distinct())
            db.Notifications.Add(
                new Notification
                {
                    RecipientUserId = recipient,
                    Type = $"Organization{next}",
                    Subject = orgSubject,
                    Body = orgBody,
                    ActionUrl = "/portal?tab=organization",
                }
            );
        db.AuditLogs.Add(
            Audit(
                principal,
                operation.Equals("reactivate", StringComparison.OrdinalIgnoreCase)
                    ? "OrganizationReactivated"
                    : $"Organization{next}",
                nameof(Organization),
                item.Id,
                old,
                next
            )
        );
        await db.SaveChangesAsync();
        return Results.Ok(item);
    }

    [HttpPost("organization-members/{id:guid}/{operation}")]
    public async Task<IResult> ChangeMemberStatus(Guid id, string operation)
    {
        var principal = User;
        var member = await db.OrganizationMembers.FindAsync(id);
        if (member is null)
            return Results.NotFound();
        if (operation == "approve")
        {
            member.MemberStatus = "Active";
            var user = await db.Users.FindAsync(member.UserId);
            if (user is not null)
                user.OrganizationId = member.OrganizationId;
        }
        else if (operation == "reject")
            member.MemberStatus = "Rejected";
        else
            return Results.BadRequest();
        db.Notifications.Add(
            new Notification
            {
                RecipientUserId = member.UserId,
                Type = "OrganizationMembershipChanged",
                Subject =
                    operation == "approve"
                        ? "Администраторот го одобри вашето барање"
                        : "Администраторот го одби вашето барање",
                Body =
                    operation == "approve"
                        ? "<p>Вашето барање за членство во организацијата е одобрено.</p>"
                        : "<p>Вашето барање за членство во организацијата е одбиено.</p>",
                ActionUrl = "/portal?tab=organization",
            }
        );
        db.AuditLogs.Add(
            Audit(principal, $"OrganizationMember{operation}", nameof(OrganizationMember), member.Id)
        );
        await db.SaveChangesAsync();
        return Results.Ok(member);
    }
}
