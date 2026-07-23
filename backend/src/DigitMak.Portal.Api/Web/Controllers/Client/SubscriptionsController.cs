using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using static DigitMak.Portal.Api.Web.Controllers.Client.ClientSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Client;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public sealed class SubscriptionsController(PortalDbContext db) : ControllerBase
{
    [HttpGet("payment-instructions")]
    public async Task<IResult> PaymentInstructions(CancellationToken ct)
    {
        string[] keys =
        [
            "PAYMENT_RECIPIENT",
            "PAYMENT_BANK",
            "PAYMENT_ACCOUNT",
            "PAYMENT_IBAN",
            "PAYMENT_SWIFT",
            "PAYMENT_AMOUNT",
            "PAYMENT_CURRENCY",
            "PAYMENT_PURPOSE",
            "PAYMENT_REFERENCE_INSTRUCTION",
            "PAYMENT_SUPPORT_EMAIL",
        ];
        var settings = await db
            .SystemSettings.Where(item => keys.Contains(item.Key))
            .ToDictionaryAsync(item => item.Key, item => item.Value, ct);
        string Value(string key) => settings.GetValueOrDefault(key)?.Trim() ?? "";
        var recipient = Value("PAYMENT_RECIPIENT");
        var account = Value("PAYMENT_ACCOUNT");
        var iban = Value("PAYMENT_IBAN");
        var amount = Value("PAYMENT_AMOUNT");
        return Results.Ok(
            new
            {
                isConfigured = recipient.Length > 0
                    && (account.Length > 0 || iban.Length > 0)
                    && amount.Length > 0,
                recipient,
                bank = Value("PAYMENT_BANK"),
                account,
                iban,
                swift = Value("PAYMENT_SWIFT"),
                amount,
                currency = Value("PAYMENT_CURRENCY"),
                purpose = Value("PAYMENT_PURPOSE"),
                referenceInstruction = Value("PAYMENT_REFERENCE_INSTRUCTION"),
                supportEmail = Value("PAYMENT_SUPPORT_EMAIL"),
            }
        );
    }

    [HttpGet("my")]
    public async Task<IResult> Mine(CancellationToken ct)
    {
        var principal = User;
        return await db
            .Subscriptions.Where(x => x.UserId == principal.UserId())
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct)
            is { } item
            ? Results.Ok(item)
            : Results.NotFound();
    }

    [HttpPost("invitations/{token}/accept")]
    [HttpPost("/api/subscription-invitations/{token}/accept")]
    public async Task<IResult> AcceptInvitationByToken(string token)
    {
        var principal = User;
        if (!principal.IsInRole("Client") || principal.IsInRole("Admin"))
            return Results.Forbid();
        var hash = Hash(token);
        var invitation = await db.SubscriptionInvitations.SingleOrDefaultAsync(x =>
            x.TokenHash == hash && x.UserId == principal.UserId()
        );
        if (
            invitation is null
            || invitation.ExpiresAt <= DateTimeOffset.UtcNow
            || invitation.Status != "Invited"
        )
            return Results.BadRequest(new { message = "Invitation is invalid or expired." });
        invitation.Status = "Accepted";
        invitation.AcceptedAt = DateTimeOffset.UtcNow;
        var item = new Subscription
        {
            UserId = invitation.UserId,
            OrganizationId = invitation.OrganizationId,
            InvitationId = invitation.Id,
            Status = "PendingPayment",
            InvitedBy = invitation.CreatedBy,
        };
        db.Add(item);
        db.AuditLogs.Add(
            Audit(
                principal,
                "SubscriptionInvitationAccepted",
                nameof(SubscriptionInvitation),
                invitation.Id
            )
        );
        await db.SaveChangesAsync();
        return Results.Created("/api/subscriptions/my", item);
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IResult> AcceptInvitationById(Guid id)
    {
        var principal = User;
        if (!principal.IsInRole("Client") || principal.IsInRole("Admin"))
            return Results.Forbid();
        var invitation = await db.SubscriptionInvitations.SingleOrDefaultAsync(x =>
            x.Id == id && x.UserId == principal.UserId()
        );
        if (invitation is null)
            return Results.NotFound();
        var rawUnavailable = invitation.TokenHash;
        if (invitation.ExpiresAt <= DateTimeOffset.UtcNow || invitation.Status != "Invited")
            return Results.BadRequest();
        invitation.Status = "Accepted";
        invitation.AcceptedAt = DateTimeOffset.UtcNow;
        var item = new Subscription
        {
            UserId = invitation.UserId,
            OrganizationId = invitation.OrganizationId,
            InvitationId = invitation.Id,
            Status = "PendingPayment",
            InvitedBy = invitation.CreatedBy,
        };
        db.Add(item);
        db.AuditLogs.Add(
            Audit(
                principal,
                "SubscriptionInvitationAccepted",
                nameof(SubscriptionInvitation),
                invitation.Id,
                rawUnavailable
            )
        );
        await db.SaveChangesAsync();
        return Results.Created("/api/subscriptions/my", item);
    }

    [HttpGet("invitations/my")]
    public async Task<IResult> MyInvitation(CancellationToken ct)
    {
        var principal = User;
        var now = DateTimeOffset.UtcNow;
        return await db
            .SubscriptionInvitations.Where(x =>
                x.UserId == principal.UserId() && x.Status == "Invited" && x.ExpiresAt > now
            )
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct)
            is { } item
            ? Results.Ok(
                new
                {
                    item.Id,
                    item.OrganizationId,
                    item.Status,
                    item.ExpiresAt,
                }
            )
            : Results.NotFound();
    }
}
