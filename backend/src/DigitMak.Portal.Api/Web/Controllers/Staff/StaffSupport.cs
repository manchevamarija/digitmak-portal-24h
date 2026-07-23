using System.Security.Claims;
using DigitMak.Portal.Api.Infrastructure.Persistence;

namespace DigitMak.Portal.Api.Web.Controllers.Staff;

/// <summary>
/// Helpers shared across staff controllers — mirrors the private static
/// helpers that used to live at the bottom of the old StaffEndpoints.cs.
/// </summary>
internal static class StaffSupport
{
    internal static readonly HashSet<string> TicketStatuses =
    [
        "New",
        "Assigned",
        "InProgress",
        "Resolved",
        "Closed",
    ];

    internal static string TicketStatusLabelMk(string status) =>
        status switch
        {
            "New" => "Нов",
            "Assigned" => "Доделен",
            "InProgress" => "Во тек",
            "Resolved" => "Решен",
            "Closed" => "Затворен",
            _ => status,
        };

    internal static AuditLog Audit(
        ClaimsPrincipal principal,
        string action,
        Guid id,
        string? oldValue = null,
        string? newValue = null
    ) =>
        new()
        {
            ActorUserId = principal.UserId(),
            Action = action,
            EntityType = action.StartsWith("Meeting", StringComparison.Ordinal)
                ? nameof(Meeting)
                : nameof(Ticket),
            EntityId = id.ToString(),
            OldValuesJson = oldValue,
            NewValuesJson = newValue,
        };

    internal static bool CanManage(Ticket ticket, ClaimsPrincipal principal) =>
        principal.IsInRole("Admin")
        || ticket.AssignedAgentId == principal.UserId()
        || ticket.AssignedExpertId == principal.UserId();

    internal static TicketMessage SystemEvent(Ticket ticket, ClaimsPrincipal principal, string body) =>
        new()
        {
            TicketId = ticket.Id,
            SenderUserId = principal.UserId(),
            MessageType = "SystemEvent",
            Body = body,
        };

    internal static IQueryable<Meeting> VisibleMeetings(PortalDbContext db, ClaimsPrincipal principal)
    {
        if (principal.IsInRole("Admin"))
            return db.Meetings;
        var userId = principal.UserId();
        return principal.IsInRole("HelpDeskAgent")
            ? db.Meetings.Where(x => x.AssignedUserId == null || x.AssignedUserId == userId)
            : db.Meetings.Where(x => x.AssignedUserId == userId);
    }
}
