using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;
using Microsoft.AspNetCore.SignalR;

namespace DigitMak.Portal.Api.Application.Realtime;

/// <summary>
/// The Infrastructure-side implementation of Application's IRealtimeTicketNotifier —
/// this is the only place that knows the transport is SignalR and that the hub is
/// called TicketHub. TicketService (Application) only ever sees the interface.
/// </summary>
public sealed class SignalRTicketNotifier(IHubContext<TicketHub> hub) : IRealtimeTicketNotifier
{
    public Task NotifyMessageCreatedAsync(Guid ticketId, TicketMessage message, CancellationToken ct) =>
        hub.Clients.Group($"ticket:{ticketId}").SendAsync("TicketMessageCreated", message, ct);
}
