using DigitMak.Portal.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitMak.Portal.Api.Web.Controllers.Notifications;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(PortalDbContext db) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<object> Mine(CancellationToken ct)
    {
        var principal = User;
        var userId = principal.UserId();
        return await db
            .Notifications.Where(x => x.RecipientUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(30)
            .Select(x => new
            {
                x.Id,
                x.Type,
                x.Subject,
                x.Body,
                x.ActionUrl,
                x.IsRead,
                x.CreatedAt,
            })
            .ToListAsync(ct);
    }

    [HttpGet("unread-count")]
    public async Task<object> UnreadCount(CancellationToken ct)
    {
        var principal = User;
        var userId = principal.UserId();
        return new
        {
            count = await db.Notifications.CountAsync(
                x => x.RecipientUserId == userId && !x.IsRead,
                ct
            ),
        };
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IResult> MarkRead(Guid id, CancellationToken ct)
    {
        var principal = User;
        var item = await db.Notifications.SingleOrDefaultAsync(
            x => x.Id == id && x.RecipientUserId == principal.UserId(),
            ct
        );
        if (item is null)
            return Results.NotFound();
        item.IsRead = true;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IResult> MarkAllRead(CancellationToken ct)
    {
        var principal = User;
        var userId = principal.UserId();
        var unread = await db
            .Notifications.Where(x => x.RecipientUserId == userId && !x.IsRead)
            .ToListAsync(ct);
        foreach (var item in unread)
            item.IsRead = true;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IResult> Delete(Guid id, CancellationToken ct)
    {
        var principal = User;
        var item = await db.Notifications.SingleOrDefaultAsync(
            x => x.Id == id && x.RecipientUserId == principal.UserId(),
            ct
        );
        if (item is null)
            return Results.NotFound();
        db.Notifications.Remove(item);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
