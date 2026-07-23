using System.Collections.Concurrent;

namespace DigitMak.Portal.Api.Application.Realtime;

/// <summary>
/// Tracks which users currently have a ticket's chat open (via a live SignalR connection),
/// so TicketService can skip sending an email notification to someone who is already looking
/// at the message in real time. Pure in-memory bookkeeping — no EF, no SignalR types — so it
/// lives in Application rather than Infrastructure; the SignalR hub (in Infrastructure) calls
/// into this same singleton on Join/Leave/Disconnect.
/// </summary>
public sealed class TicketPresenceTracker
{
    private readonly ConcurrentDictionary<string, (Guid TicketId, Guid UserId)> connections = new();

    public void Join(string connectionId, Guid ticketId, Guid userId) =>
        connections[connectionId] = (ticketId, userId);

    public void Leave(string connectionId, Guid ticketId)
    {
        if (connections.TryGetValue(connectionId, out var current) && current.TicketId == ticketId)
            connections.TryRemove(connectionId, out _);
    }

    public void Disconnect(string connectionId) => connections.TryRemove(connectionId, out _);

    public bool IsPresent(Guid ticketId, Guid userId) =>
        connections.Values.Any(x => x.TicketId == ticketId && x.UserId == userId);
}
