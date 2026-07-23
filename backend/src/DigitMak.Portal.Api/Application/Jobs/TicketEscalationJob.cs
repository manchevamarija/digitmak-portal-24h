using Microsoft.EntityFrameworkCore;
using Quartz;
using DigitMak.Portal.Api.Infrastructure.Persistence;

namespace DigitMak.Portal.Api.Application.Jobs;

/// <summary>
/// Urgent/High priority tickets already page staff immediately on creation (see
/// TicketService.CreateAsync) — that keeps the notification bell quiet for the routine
/// majority of tickets. But a Normal/Low priority ticket that nobody has picked up after a
/// full day is exactly the kind of thing that quietly falls through the cracks, so this job
/// escalates it once, 24 hours after creation, if it is still sitting in "New" status.
/// </summary>
[DisallowConcurrentExecution]
public sealed class TicketEscalationJob(PortalDbContext db) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddHours(-24);

        var overdueTickets = await db
            .Tickets.Where(ticket =>
                ticket.Status == "New"
                && (ticket.Priority == "Normal" || ticket.Priority == "Low")
                && ticket.CreatedAt <= cutoff
            )
            .ToListAsync(ct);
        if (overdueTickets.Count == 0)
            return;

        var staffUserIds = await db
            .UserRoles.Join(
                db.Roles.Where(role => role.Name == "Admin" || role.Name == "HelpDeskAgent"),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, _) => userRole.UserId
            )
            .Distinct()
            .ToListAsync(ct);

        foreach (var ticket in overdueTickets)
        {
            var alreadyEscalated = await db.Notifications.AnyAsync(
                x => x.Type == "TicketEscalated" && x.ActionUrl == $"/staff?tab=tickets&ticket={ticket.Id}",
                ct
            );
            if (alreadyEscalated)
                continue;

            var hoursWaiting = (int)(now - ticket.CreatedAt).TotalHours;
            foreach (var staffUserId in staffUserIds)
                db.Notifications.Add(
                    new Notification
                    {
                        RecipientUserId = staffUserId,
                        Type = "TicketEscalated",
                        Subject = $"Нема одговор на тикет {ticket.TicketNumber}",
                        Body =
                            $"<p>Тикетот <strong>{ticket.Title}</strong> е креиран пред {hoursWaiting} часа и сеуште нема одговор.</p>",
                        ActionUrl = $"/staff?tab=tickets&ticket={ticket.Id}",
                    }
                );
        }
        await db.SaveChangesAsync(ct);
    }
}
