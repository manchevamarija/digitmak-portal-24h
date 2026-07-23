using DigitMak.Portal.Api.Application.Realtime;
using Microsoft.AspNetCore.SignalR;
using DigitMak.Portal.Api.Infrastructure.Persistence;

namespace DigitMak.Portal.Api.Application.Realtime;

public class TicketHub(PortalDbContext db, TicketPresenceTracker presence) : Hub
{
    public async Task JoinTicket(Guid ticketId)
    {
        var userId = Guid.Parse(Context.UserIdentifier!);
        var t = await db.Tickets.FindAsync(ticketId) ?? throw new HubException("Ticket not found");
        if (
            t.CreatedByUserId != userId
            && t.AssignedAgentId != userId
            && t.AssignedExpertId != userId
            && !(Context.User?.IsInRole("Admin") ?? false)
        )
            throw new HubException("Forbidden");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket:{ticketId}");
        presence.Join(Context.ConnectionId, ticketId, userId);
    }

    public async Task LeaveTicket(Guid ticketId)
    {
        presence.Leave(Context.ConnectionId, ticketId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket:{ticketId}");
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        presence.Disconnect(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
