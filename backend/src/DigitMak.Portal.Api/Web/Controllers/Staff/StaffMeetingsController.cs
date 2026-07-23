using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;
using static DigitMak.Portal.Api.Web.Controllers.Staff.StaffSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Staff;

[ApiController]
[Route("api/staff/meetings")]
[Authorize(Policy = "Staff")]
public sealed class StaffMeetingsController(PortalDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<Meeting>> Get(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var principal = User;
        return await VisibleMeetings(db, principal)
            .Where(x => (from == null || x.StartsAt >= from) && (to == null || x.StartsAt <= to))
            .OrderBy(x => x.StartsAt)
            .ToListAsync(ct);
    }

    [HttpGet("calendar.ics")]
    public async Task<IResult> CalendarIcs(CancellationToken ct)
    {
        var principal = User;
        return Results.File(
            CalendarExport.Ics(
                await VisibleMeetings(db, principal)
                    .Where(x => x.StartsAt != null)
                    .OrderBy(x => x.StartsAt)
                    .ToListAsync(ct)
            ),
            "text/calendar; charset=utf-8",
            "digitmak-staff-calendar.ics"
        );
    }

    [HttpPost("{id:guid}/{operation}")]
    public async Task<IResult> Decide(Guid id, string operation, MeetingDecisionRequest request)
    {
        var principal = User;
        var item = await db.Meetings.FindAsync(id);
        if (item is null)
            return Results.NotFound();
        var canTriage =
            principal.IsInRole("Admin")
            || (principal.IsInRole("HelpDeskAgent") && item.AssignedUserId is null);
        if (!canTriage && item.AssignedUserId != principal.UserId())
            return Results.Forbid();
        var next = operation.ToLowerInvariant() switch
        {
            "confirm" => "Confirmed",
            "reject" => "Rejected",
            "complete" => "Completed",
            "propose" => "Requested",
            _ => null,
        };
        if (next is null)
            return Results.BadRequest();
        item.Status = next;
        item.StartsAt = request.StartsAt ?? item.StartsAt;
        item.EndsAt = request.EndsAt ?? item.EndsAt;
        item.Location = request.Location ?? item.Location;
        item.OnlineLink = request.OnlineLink ?? item.OnlineLink;
        item.Notes = request.Notes ?? item.Notes;
        item.AssignedUserId = request.AssignedUserId ?? item.AssignedUserId ?? principal.UserId();
        if (next == "Confirmed")
        {
            item.ConfirmedBy = principal.UserId();
            item.ConfirmedAt = DateTimeOffset.UtcNow;
        }
        var (meetingSubject, meetingBody) = next switch
        {
            "Confirmed" => (
                "Администраторот го закажа вашиот состанок",
                $"<p>Состанокот <strong>{item.Subject}</strong> е потврден.</p>"
            ),
            "Rejected" => (
                "Администраторот го одби барањето за состанок",
                $"<p>Барањето за состанок <strong>{item.Subject}</strong> е одбиено.</p>"
            ),
            "Completed" => (
                "Состанокот е завршен",
                $"<p>Состанокот <strong>{item.Subject}</strong> е означен како завршен.</p>"
            ),
            _ => (
                "Предложен е нов термин за состанокот",
                $"<p>Предложен е нов термин за состанокот <strong>{item.Subject}</strong>.</p>"
            ),
        };
        db.Notifications.Add(
            new Notification
            {
                RecipientUserId = item.RequestedByUserId,
                Type = $"Meeting{next}",
                Subject = meetingSubject,
                Body = meetingBody,
                ActionUrl = "/portal?tab=meetings",
            }
        );
        db.AuditLogs.Add(Audit(principal, $"Meeting{next}", item.Id));
        await db.SaveChangesAsync();
        return Results.Ok(item);
    }
}
