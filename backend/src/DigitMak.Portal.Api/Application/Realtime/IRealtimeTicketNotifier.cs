using DigitMak.Portal.Api.Application.Realtime;

namespace DigitMak.Portal.Api.Application.Realtime;

/// <summary>
/// Lets TicketService push a real-time update without knowing the transport is SignalR
/// (or that the concrete Hub class is called TicketHub). SignalRTicketNotifier below
/// supplies the implementation by wrapping IHubContext&lt;TicketHub&gt;.
/// </summary>
public interface IRealtimeTicketNotifier
{
    Task NotifyMessageCreatedAsync(Guid ticketId, TicketMessage message, CancellationToken ct);
}
