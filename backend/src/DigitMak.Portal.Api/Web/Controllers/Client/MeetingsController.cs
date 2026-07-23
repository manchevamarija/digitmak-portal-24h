using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static DigitMak.Portal.Api.Web.Controllers.Client.ClientSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Client;

[ApiController]
[Route("api/meetings")]
[Authorize]
public sealed class MeetingsController(IMeetingService service, PortalDbContext db) : ControllerBase
{
    [HttpGet("my")]
    public async Task<object> Mine(CancellationToken ct)
    {
        var principal = User;
        return await service.GetMineAsync(principal.UserId(), ct);
    }

    [HttpGet("calendar.ics")]
    public async Task<IResult> CalendarIcs(CancellationToken ct)
    {
        var principal = User;
        return Results.File(
            CalendarExport.Ics(
                await db
                    .Meetings.Where(x => x.RequestedByUserId == principal.UserId() && x.StartsAt != null)
                    .OrderBy(x => x.StartsAt)
                    .ToListAsync(ct)
            ),
            "text/calendar; charset=utf-8",
            "digitmak-meetings.ics"
        );
    }

    [HttpPost]
    public async Task<IResult> Create(MeetingRequest request, CancellationToken ct)
    {
        var principal = User;
        if (await service.CreateAsync(request, principal.UserId(), principal.UserId(), ct) is not { } item)
            return Results.Forbid();
        await NotifyAdminsAsync(
            db,
            "MeetingRequested",
            $"Нов состанок побаран: {item.Subject}",
            $"<p>Состанок „{item.Subject}“ е побаран и чека потврда.</p>",
            "/staff?tab=meetings",
            ct
        );
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/meetings/{item.Id}", item);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IResult> Cancel(Guid id)
    {
        var principal = User;
        var item = await db.Meetings.SingleOrDefaultAsync(x =>
            x.Id == id && x.RequestedByUserId == principal.UserId()
        );
        if (item is null)
            return Results.NotFound();
        if (item.Status is "Completed" or "Cancelled")
            return Results.Conflict();
        item.Status = "Cancelled";
        db.Notifications.Add(
            new Notification
            {
                RecipientUserId = item.AssignedUserId,
                Type = "MeetingCancelled",
                Subject = "Клиентот го откажа состанокот",
                Body = $"<p>Состанокот „{item.Subject}“ е откажан.</p>",
                ActionUrl = "/staff?tab=meetings",
            }
        );
        db.AuditLogs.Add(Audit(principal, "MeetingCancelled", nameof(Meeting), item.Id));
        await db.SaveChangesAsync();
        return Results.Ok(item);
    }

    [HttpPost("{id:guid}/reschedule")]
    public async Task<IResult> Reschedule(
        Guid id,
        MeetingRescheduleRequest request,
        CancellationToken ct
    )
    {
        var principal = User;
        var item = await db.Meetings.SingleOrDefaultAsync(
            x => x.Id == id && x.RequestedByUserId == principal.UserId(),
            ct
        );
        if (item is null)
            return Results.NotFound();
        if (item.Status == "Completed")
            return Results.Conflict(new { message = "A completed meeting cannot be rescheduled." });
        if (
            request.PreferredStart is not null
            && request.PreferredEnd is not null
            && request.PreferredEnd <= request.PreferredStart
        )
            return Results.BadRequest(new { message = "The proposed end must be after the start." });

        item.StartsAt = request.PreferredStart;
        item.EndsAt = request.PreferredEnd;
        item.RequestedTimeWindow = request.RequestedTimeWindow?.Trim();
        item.Notes = request.Notes?.Trim();
        item.Status = "Requested";
        item.ConfirmedBy = null;
        item.ConfirmedAt = null;
        db.Notifications.Add(
            new Notification
            {
                RecipientUserId = item.AssignedUserId,
                Type = "MeetingRescheduleRequested",
                Subject = "Клиентот побара промена на термин",
                Body = $"<p>Клиентот побара промена на термин за состанокот <strong>{item.Subject}</strong>.</p>",
                ActionUrl = "/staff?tab=meetings",
            }
        );
        db.AuditLogs.Add(Audit(principal, "MeetingRescheduleRequested", nameof(Meeting), item.Id));
        await db.SaveChangesAsync(ct);
        return Results.Ok(item);
    }
}
