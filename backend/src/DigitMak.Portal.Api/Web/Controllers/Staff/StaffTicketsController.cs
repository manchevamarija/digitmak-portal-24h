using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Application.Realtime;
using static DigitMak.Portal.Api.Web.Controllers.Staff.StaffSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Staff;

[ApiController]
[Route("api/staff/tickets")]
[Authorize(Policy = "Staff")]
public sealed class StaffTicketsController(
    PortalDbContext db,
    UserManager<AppUser> users,
    IHubContext<TicketHub> hub,
    ITicketService service
) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<Ticket>> Get(int? page, int? pageSize, CancellationToken ct)
    {
        var principal = User;
        var userId = principal.UserId();
        var query = db.Tickets.AsQueryable();
        if (!principal.IsInRole("Admin"))
            query = principal.IsInRole("HelpDeskAgent")
                ? query.Where(x => x.AssignedAgentId == null || x.AssignedAgentId == userId)
                : query.Where(x => x.AssignedExpertId == userId);
        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((Math.Max(page ?? 1, 1) - 1) * Math.Clamp(pageSize ?? 50, 1, 100))
            .Take(Math.Clamp(pageSize ?? 50, 1, 100))
            .ToListAsync(ct);
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<IResult> Assign(Guid id, TicketAssignmentRequest request)
    {
        var principal = User;
        if (!principal.IsInRole("Admin") && !principal.IsInRole("HelpDeskAgent"))
            return Results.Forbid();
        var item = await db.Tickets.FindAsync(id);
        if (item is null)
            return Results.NotFound();
        if (request.AgentId is not null)
        {
            var agent = await users.FindByIdAsync(request.AgentId.Value.ToString());
            if (
                agent is null
                || !(await users.IsInRoleAsync(agent, "HelpDeskAgent"))
                    && !(await users.IsInRoleAsync(agent, "Admin"))
            )
                return Results.BadRequest(new { message = "Invalid help-desk agent." });
        }
        if (request.ExpertId is not null)
        {
            var expert = await users.FindByIdAsync(request.ExpertId.Value.ToString());
            if (expert is null || !(await users.IsInRoleAsync(expert, "Expert")))
                return Results.BadRequest(new { message = "Invalid expert." });
        }
        item.AssignedAgentId = request.AgentId;
        item.AssignedExpertId = request.ExpertId;
        item.Status = "Assigned";
        var systemEvent = SystemEvent(item, principal, "TICKET_ASSIGNED");
        db.TicketMessages.Add(systemEvent);
        db.Notifications.Add(
            new Notification
            {
                RecipientUserId = item.CreatedByUserId,
                Type = "TicketAssigned",
                Subject = $"Тикетот {item.TicketNumber} е доделен",
                Body = "<p>Вашиот тикет е доделен на тимот на DigitMak.</p>",
                ActionUrl = $"/portal?tab=tickets&ticket={item.Id}",
            }
        );
        foreach (
            var recipient in new[] { request.AgentId, request.ExpertId }
                .Where(x => x is not null && x != principal.UserId())
                .Select(x => x!.Value)
                .Distinct()
        )
            db.Notifications.Add(
                new Notification
                {
                    RecipientUserId = recipient,
                    Type = "TicketAssigned",
                    Subject = $"Доделен ви е тикет {item.TicketNumber}",
                    Body = $"<p>Ви е доделен тикетот <strong>{item.Title}</strong>.</p>",
                    ActionUrl = $"/staff?tab=tickets&ticket={item.Id}",
                }
            );
        db.AuditLogs.Add(Audit(principal, "TicketAssigned", item.Id));
        await db.SaveChangesAsync();
        await hub.Clients.Group($"ticket:{id}").SendAsync("TicketAssigned", item);
        await hub.Clients.Group($"ticket:{id}").SendAsync("TicketMessageCreated", systemEvent);
        return Results.Ok(item);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IResult> ChangeStatus(Guid id, string status)
    {
        var principal = User;
        if (!TicketStatuses.Contains(status))
            return Results.BadRequest(new { message = "Invalid ticket status." });
        var item = await db.Tickets.FindAsync(id);
        if (item is null)
            return Results.NotFound();
        if (!CanManage(item, principal))
            return Results.Forbid();
        var old = item.Status;
        item.Status = status;
        if (status == "Resolved")
            item.ResolvedAt = DateTimeOffset.UtcNow;
        if (status == "Closed")
            item.ClosedAt = DateTimeOffset.UtcNow;
        var systemEvent = SystemEvent(item, principal, $"TICKET_STATUS_CHANGED:{old}:{status}");
        db.TicketMessages.Add(systemEvent);
        db.Notifications.Add(
            new Notification
            {
                RecipientUserId = item.CreatedByUserId,
                Type = "TicketStatusChanged",
                Subject = $"Тикетот {item.TicketNumber}: {TicketStatusLabelMk(status)}",
                Body =
                    $"<p>Статусот на вашиот тикет се промени од „{TicketStatusLabelMk(old)}“ во „{TicketStatusLabelMk(status)}“.</p>",
                ActionUrl = $"/portal?tab=tickets&ticket={item.Id}",
            }
        );
        db.AuditLogs.Add(Audit(principal, "TicketStatusChanged", item.Id, old, status));
        await db.SaveChangesAsync();
        await hub.Clients.Group($"ticket:{id}").SendAsync("TicketStatusChanged", new { id, status });
        await hub.Clients.Group($"ticket:{id}").SendAsync("TicketMessageCreated", systemEvent);
        return Results.Ok(item);
    }

    [HttpPatch("{id:guid}/recommendation")]
    public async Task<IResult> UpdateRecommendation(Guid id, TicketRecommendationRequest request)
    {
        var principal = User;
        var item = await db.Tickets.FindAsync(id);
        if (item is null)
            return Results.NotFound();
        if (!CanManage(item, principal))
            return Results.Forbid();
        item.FinalRecommendation = request.FinalRecommendation;
        item.ReferralRecommendation = request.ReferralRecommendation;
        db.TicketMessages.Add(SystemEvent(item, principal, "TICKET_RECOMMENDATION_UPDATED"));
        db.AuditLogs.Add(Audit(principal, "TicketRecommendationUpdated", item.Id));
        await db.SaveChangesAsync();
        return Results.Ok(item);
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<IResult> Resolve(Guid id, TicketRecommendationRequest request)
    {
        var principal = User;
        var item = await db.Tickets.FindAsync(id);
        if (item is null)
            return Results.NotFound();
        if (!CanManage(item, principal))
            return Results.Forbid();
        item.FinalRecommendation = request.FinalRecommendation;
        item.ReferralRecommendation = request.ReferralRecommendation;
        item.Status = "Resolved";
        item.ResolvedAt = DateTimeOffset.UtcNow;
        var systemEvent = SystemEvent(item, principal, "TICKET_RESOLVED");
        db.TicketMessages.Add(systemEvent);
        db.Notifications.Add(
            new Notification
            {
                RecipientUserId = item.CreatedByUserId,
                Type = "TicketResolved",
                Subject = $"Тикетот {item.TicketNumber} е решен",
                ActionUrl = $"/portal?tab=tickets&ticket={item.Id}",
                Body =
                    "<p>Вашиот тикет е решен. Отворете го порталот за да ја прегледате препораката.</p>",
            }
        );
        db.AuditLogs.Add(Audit(principal, "TicketResolved", item.Id));
        await db.SaveChangesAsync();
        await hub
            .Clients.Group($"ticket:{id}")
            .SendAsync("TicketStatusChanged", new { id, status = item.Status });
        await hub.Clients.Group($"ticket:{id}").SendAsync("TicketMessageCreated", systemEvent);
        return Results.Ok(item);
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IResult> Close(Guid id)
    {
        var principal = User;
        var item = await db.Tickets.FindAsync(id);
        if (item is null)
            return Results.NotFound();
        if (!CanManage(item, principal))
            return Results.Forbid();
        item.Status = "Closed";
        item.ClosedAt = DateTimeOffset.UtcNow;
        var systemEvent = SystemEvent(item, principal, "TICKET_CLOSED");
        db.TicketMessages.Add(systemEvent);
        db.AuditLogs.Add(Audit(principal, "TicketClosed", item.Id));
        await db.SaveChangesAsync();
        await hub
            .Clients.Group($"ticket:{id}")
            .SendAsync("TicketStatusChanged", new { id, status = item.Status });
        await hub.Clients.Group($"ticket:{id}").SendAsync("TicketMessageCreated", systemEvent);
        return Results.Ok(item);
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<IResult> Reply(Guid id, MessageRequest request, CancellationToken ct)
    {
        var principal = User;
        return await service.AddMessageAsync(id, request.Body, "StaffReply", principal, ct)
            is { } item
            ? Results.Created($"/api/tickets/{id}/messages/{item.Id}", item)
            : Results.NotFound();
    }

    [HttpPost("{id:guid}/internal-notes")]
    public async Task<IResult> AddInternalNote(Guid id, MessageRequest request, CancellationToken ct)
    {
        var principal = User;
        return await service.AddMessageAsync(id, request.Body, "InternalNote", principal, ct)
            is { } item
            ? Results.Created($"/api/tickets/{id}/messages/{item.Id}", item)
            : Results.NotFound();
    }
}
