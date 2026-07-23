using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static DigitMak.Portal.Api.Web.Controllers.Client.ClientSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Client;

[ApiController]
[Route("api/tickets")]
[Authorize]
public sealed class TicketsController(
    ITicketService service,
    IMeetingService meetings,
    IFileStorage storage,
    PortalDbContext db
) : ControllerBase
{
    [HttpGet("my")]
    public async Task<object> Mine(CancellationToken ct)
    {
        var principal = User;
        return await service.GetMineAsync(principal.UserId(), ct);
    }

    [HttpPost]
    public async Task<IResult> Create(TicketRequest request, CancellationToken ct)
    {
        var principal = User;
        return await service.CreateAsync(request, principal.UserId(), ct) is { } item
            ? Results.Created($"/api/tickets/{item.Id}", item)
            : Results.Json(
                new
                {
                    code = "TICKET_ACCESS_REQUIRED",
                    message = "An approved organization and an active, unexpired personal subscription are required to create a ticket.",
                },
                statusCode: StatusCodes.Status403Forbidden
            );
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> Get(Guid id, CancellationToken ct)
    {
        var principal = User;
        return await service.GetVisibleAsync(id, principal, ct) is { } item
            ? Results.Ok(item)
            : Results.NotFound();
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<IResult> Messages(Guid id, CancellationToken ct)
    {
        var principal = User;
        return Results.Ok(await service.GetMessagesAsync(id, principal, ct));
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<IResult> AddMessage(Guid id, MessageRequest request, CancellationToken ct)
    {
        var principal = User;
        return await service.AddMessageAsync(id, request.Body, "ClientMessage", principal, ct)
            is { } item
            ? Results.Created($"/api/tickets/{id}/messages/{item.Id}", item)
            : Results.NotFound();
    }

    [HttpGet("{id:guid}/attachments")]
    public async Task<IResult> Attachments(Guid id, CancellationToken ct)
    {
        var principal = User;
        if (await service.GetVisibleAsync(id, principal, ct) is null)
            return Results.NotFound();
        var items = await db
            .TicketAttachments.Where(x => x.TicketId == id)
            .Join(
                db.Files,
                x => x.FileId,
                x => x.Id,
                (attachment, file) =>
                    new
                    {
                        attachment.Id,
                        attachment.TicketId,
                        attachment.MessageId,
                        attachment.FileId,
                        attachment.UploadedBy,
                        attachment.CreatedAt,
                        file.OriginalFilename,
                        file.ContentType,
                        file.SizeBytes,
                        file.Checksum,
                    }
            )
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
        return Results.Ok(items);
    }

    [HttpPost("{id:guid}/attachments")]
    public async Task<IResult> UploadAttachment(
        Guid id,
        Guid? messageId,
        IFormFile file,
        CancellationToken ct
    )
    {
        var principal = User;
        if (await service.GetVisibleAsync(id, principal, ct) is null)
            return Results.NotFound();
        if (
            messageId is not null
            && !await db.TicketMessages.AnyAsync(x => x.Id == messageId && x.TicketId == id, ct)
        )
            return Results.BadRequest(
                new { message = "The selected message does not belong to this ticket." }
            );
        try
        {
            var stored = await storage.SaveAsync(file, nameof(Ticket), id, principal.UserId(), ct);
            var attachment = new TicketAttachment
            {
                TicketId = id,
                MessageId = messageId,
                FileId = stored.Id,
                UploadedBy = principal.UserId(),
            };
            db.TicketAttachments.Add(attachment);
            db.AuditLogs.Add(
                Audit(
                    principal,
                    "TicketAttachmentUploaded",
                    nameof(TicketAttachment),
                    attachment.Id
                )
            );
            await db.SaveChangesAsync(ct);
            return Results.Created(
                $"/api/tickets/{id}/attachments/{attachment.Id}",
                new
                {
                    attachment.Id,
                    attachment.TicketId,
                    attachment.MessageId,
                    attachment.FileId,
                    attachment.UploadedBy,
                    attachment.CreatedAt,
                    stored.OriginalFilename,
                    stored.ContentType,
                    stored.SizeBytes,
                    stored.Checksum,
                }
            );
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/meetings")]
    public async Task<IResult> RequestMeeting(Guid id, MeetingRequest request, CancellationToken ct)
    {
        var principal = User;
        return await meetings.CreateAsync(request with { TicketId = id }, principal.UserId(), principal.UserId(), ct)
            is { } item
            ? Results.Created($"/api/meetings/{item.Id}", item)
            : Results.Forbid();
    }
}
