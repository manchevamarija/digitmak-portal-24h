using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using static DigitMak.Portal.Api.Web.Controllers.Admin.AdminSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Admin;

[ApiController]
[Route("api/admin/account-change-requests")]
[Authorize(Policy = "Admin")]
public sealed class AdminAccountChangeRequestsController(PortalDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<object> Get(CancellationToken ct) =>
        await db
            .AccountChangeRequests.Join(
                db.Users,
                request => request.UserId,
                user => user.Id,
                (request, user) =>
                    new
                    {
                        request.Id,
                        request.UserId,
                        request.OrganizationId,
                        request.RequestType,
                        request.Details,
                        request.Status,
                        request.DecisionNote,
                        request.DecidedAt,
                        request.CreatedAt,
                        user.Email,
                        user.FirstName,
                        user.LastName,
                    }
            )
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    [HttpPost("{id:guid}/decision")]
    public async Task<IResult> Decide(Guid id, AccountChangeDecisionRequest request, CancellationToken ct)
    {
        var principal = User;
        if (request.Status is not ("Accepted" or "Declined"))
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["status"] = ["Choose Accepted or Declined."] }
            );
        var item = await db.AccountChangeRequests.FindAsync([id], ct);
        if (item is null)
            return Results.NotFound();
        if (item.Status != "Pending")
            return Results.Conflict(new { message = "The request is already decided." });
        item.Status = request.Status;
        item.DecisionNote = request.Note?.Trim();
        item.DecidedBy = principal.UserId();
        item.DecidedAt = DateTimeOffset.UtcNow;
        var preferredLanguage =
            await db
                .Users.Where(x => x.Id == item.UserId)
                .Select(x => x.PreferredLanguage)
                .SingleOrDefaultAsync(ct) ?? "mk";
        var (subject, body) = (preferredLanguage, item.Status) switch
        {
            ("mk", "Accepted") => (
                "Одобрено барање за промена",
                $"<p>Вашето барање за промена на {item.RequestType.ToLowerInvariant()} е одобрено.</p><p>{item.DecisionNote}</p>"
            ),
            ("mk", _) => (
                "Одбиено барање за промена",
                $"<p>Вашето барање за промена на {item.RequestType.ToLowerInvariant()} е одбиено.</p><p>{item.DecisionNote}</p>"
            ),
            ("sq", "Accepted") => (
                "Kërkesa për ndryshim u miratua",
                $"<p>Kërkesa juaj për ndryshim u miratua.</p><p>{item.DecisionNote}</p>"
            ),
            ("sq", _) => (
                "Kërkesa për ndryshim u refuzua",
                $"<p>Kërkesa juaj për ndryshim u refuzua.</p><p>{item.DecisionNote}</p>"
            ),
            (_, "Accepted") => (
                "Account change request approved",
                $"<p>Your {item.RequestType.ToLowerInvariant()} change request was approved.</p><p>{item.DecisionNote}</p>"
            ),
            _ => (
                "Account change request declined",
                $"<p>Your {item.RequestType.ToLowerInvariant()} change request was declined.</p><p>{item.DecisionNote}</p>"
            ),
        };
        db.Notifications.Add(
            new Notification
            {
                RecipientUserId = item.UserId,
                Type = $"AccountChange{item.Status}",
                Language = preferredLanguage,
                Subject = subject,
                Body = body,
                ActionUrl = "/portal?tab=organization",
            }
        );
        db.AuditLogs.Add(
            Audit(principal, $"AccountChange{item.Status}", nameof(AccountChangeRequest), item.Id)
        );
        await db.SaveChangesAsync(ct);
        return Results.Ok(item);
    }
}
