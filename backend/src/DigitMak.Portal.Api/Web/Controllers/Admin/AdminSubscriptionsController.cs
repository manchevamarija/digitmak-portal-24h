using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using static DigitMak.Portal.Api.Web.Controllers.Admin.AdminSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "Admin")]
public sealed class AdminSubscriptionsController(PortalDbContext db, IConfiguration config)
    : ControllerBase
{
    [HttpPost("subscription-invitations")]
    public async Task<IResult> Invite(SubscriptionInviteRequest request)
    {
        var principal = User;
        if (
            !await db.Users.AnyAsync(x =>
                x.Id == request.UserId && x.OrganizationId == request.OrganizationId
            )
            || !await db.Organizations.AnyAsync(x =>
                x.Id == request.OrganizationId && x.Status == "Approved"
            )
        )
            return Results.BadRequest(new { message = "User and approved organization do not match." });
        if (await db.Subscriptions.AnyAsync(x => x.UserId == request.UserId && x.Status == "Active"))
            return Results.Conflict(new { message = "User already has an active subscription." });
        var raw = Convert
            .ToBase64String(RandomNumberGenerator.GetBytes(36))
            .Replace("/", "_")
            .Replace("+", "-");
        var item = new SubscriptionInvitation
        {
            UserId = request.UserId,
            OrganizationId = request.OrganizationId,
            TokenHash = Hash(raw),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(14),
            CreatedBy = principal.UserId(),
        };
        db.Add(item);
        var root = (config["APP_PUBLIC_URL"] ?? "http://localhost:5173").TrimEnd('/');
        var link = $"{root}/subscription-invite?token={Uri.EscapeDataString(raw)}";
        db.Notifications.Add(
            new Notification
            {
                RecipientUserId = request.UserId,
                Type = "SubscriptionInvitation",
                Subject = "Покана за годишна претплата",
                ActionUrl = "/portal?tab=organization",
                Body =
                    $"<p>Вашата годишна претплата е подготвена.</p><p><a href=\"{link}\">Прифати ја поканата</a></p>",
            }
        );
        db.AuditLogs.Add(
            Audit(principal, "SubscriptionInvitationCreated", nameof(SubscriptionInvitation), item.Id)
        );
        await db.SaveChangesAsync();
        return Results.Created(
            $"/api/admin/subscription-invitations/{item.Id}",
            new
            {
                item.Id,
                item.UserId,
                item.OrganizationId,
                item.Status,
                item.ExpiresAt,
            }
        );
    }

    [HttpGet("subscription-invitations")]
    public async Task<IReadOnlyList<SubscriptionInvitation>> GetInvitations(CancellationToken ct) =>
        await db.SubscriptionInvitations.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);

    [HttpGet("subscriptions")]
    public async Task<IReadOnlyList<Subscription>> Get(CancellationToken ct)
    {
        var clientRoleUserIds = db.UserRoles.Join(
            db.Roles.Where(role => role.Name == "Client"),
            userRole => userRole.RoleId,
            role => role.Id,
            (userRole, _) => userRole.UserId
        );
        var privilegedUserIds = db.UserRoles.Join(
            db.Roles.Where(role => role.Name == "Admin"),
            userRole => userRole.RoleId,
            role => role.Id,
            (userRole, _) => userRole.UserId
        );
        var clientUserIds = clientRoleUserIds.Where(userId => !privilegedUserIds.Contains(userId));
        return await db
            .Subscriptions.Where(item => clientUserIds.Contains(item.UserId))
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(ct);
    }

    [HttpPost("subscriptions/{id:guid}/activate")]
    public async Task<IResult> Activate(Guid id, SubscriptionActivationRequest request)
    {
        var principal = User;
        var item = await db.Subscriptions.FindAsync(id);
        if (item is null)
            return Results.NotFound();
        if (item.Status != "PendingPayment")
            return Results.Conflict(new { message = "Subscription is not pending payment." });
        item.Status = "Active";
        item.StartsAt = DateTimeOffset.UtcNow;
        item.ExpiresAt = DateTimeOffset.UtcNow.AddMonths(12);
        item.OfflinePaymentReference = request.PaymentReference;
        item.PaymentNote = request.PaymentNote;
        item.ActivatedBy = principal.UserId();
        item.ActivatedAt = DateTimeOffset.UtcNow;
        db.Notifications.Add(
            new Notification
            {
                RecipientUserId = item.UserId,
                Type = "SubscriptionActivated",
                Subject = "Администраторот ја активираше вашата претплата",
                Body = $"<p>Вашата претплата е активна до {item.ExpiresAt:yyyy-MM-dd}.</p>",
                ActionUrl = "/portal?tab=organization",
            }
        );
        db.AuditLogs.Add(Audit(principal, "SubscriptionActivated", nameof(Subscription), item.Id));
        await db.SaveChangesAsync();
        return Results.Ok(item);
    }

    [HttpPost("subscriptions/{id:guid}/{operation}")]
    public async Task<IResult> ChangeStatus(Guid id, string operation)
    {
        var principal = User;
        var item = await db.Subscriptions.FindAsync(id);
        if (item is null)
            return Results.NotFound();
        if (operation == "cancel")
        {
            item.Status = "Cancelled";
            item.CancelledBy = principal.UserId();
            item.CancelledAt = DateTimeOffset.UtcNow;
        }
        else if (operation == "expire")
        {
            item.Status = "Expired";
            item.ExpiresAt = DateTimeOffset.UtcNow;
        }
        else
            return Results.BadRequest();
        db.Notifications.Add(
            new Notification
            {
                RecipientUserId = item.UserId,
                Type = $"Subscription{item.Status}",
                Subject =
                    operation == "cancel"
                        ? "Администраторот ја откажа вашата претплата"
                        : "Вашата претплата истече",
                Body =
                    operation == "cancel"
                        ? "<p>Вашата претплата е откажана.</p>"
                        : "<p>Вашата претплата истече. Обратете се до администратор за продолжување.</p>",
                ActionUrl = "/portal?tab=organization",
            }
        );
        db.AuditLogs.Add(Audit(principal, $"Subscription{item.Status}", nameof(Subscription), item.Id));
        await db.SaveChangesAsync();
        return Results.Ok(item);
    }
}
