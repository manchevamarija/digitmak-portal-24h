using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;

namespace DigitMak.Portal.Api.Web.Controllers.Files;

[ApiController]
[Route("api/files")]
[Authorize]
public sealed class FilesController(IFileStorage storage, PortalDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IResult> Upload(IFormFile file, string entityType, Guid entityId, CancellationToken ct)
    {
        var principal = User;
        if (
            !principal.IsInRole("Admin")
            && !principal.IsInRole("HelpDeskAgent")
            && !principal.IsInRole("Expert")
        )
            return Results.Forbid();
        try
        {
            var stored = await storage.SaveAsync(file, entityType, entityId, principal.UserId(), ct);
            db.AuditLogs.Add(
                new AuditLog
                {
                    ActorUserId = principal.UserId(),
                    Action = "FileUploaded",
                    EntityType = nameof(FileObject),
                    EntityId = stored.Id.ToString(),
                }
            );
            await db.SaveChangesAsync(ct);
            return Results.Ok(stored);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> Download(Guid id, CancellationToken ct)
    {
        var principal = User;
        var result = await storage.OpenAsync(id, ct);
        if (result is null)
            return Results.NotFound();
        var staff =
            principal.IsInRole("Admin")
            || principal.IsInRole("HelpDeskAgent")
            || principal.IsInRole("Expert");
        var ticketId = await db
            .TicketAttachments.Where(x => x.FileId == id)
            .Select(x => (Guid?)x.TicketId)
            .SingleOrDefaultAsync(ct);
        var ticketAccess =
            ticketId is not null
            && await db.Tickets.AnyAsync(
                x =>
                    x.Id == ticketId
                    && (
                        x.CreatedByUserId == principal.UserId()
                        || x.AssignedAgentId == principal.UserId()
                        || x.AssignedExpertId == principal.UserId()
                    ),
                ct
            );
        if (!staff && !ticketAccess && result.Value.File.UploadedBy != principal.UserId())
        {
            result.Value.Stream.Dispose();
            return Results.Forbid();
        }
        return Results.File(
            result.Value.Stream,
            result.Value.File.ContentType,
            result.Value.File.OriginalFilename
        );
    }

    [HttpDelete("{id:guid}")]
    public async Task<IResult> Delete(Guid id, CancellationToken ct)
    {
        var principal = User;
        var opened = await storage.OpenAsync(id, ct);
        if (opened is null)
            return Results.NotFound();
        opened.Value.Stream.Dispose();
        if (!principal.IsInRole("Admin") && opened.Value.File.UploadedBy != principal.UserId())
            return Results.Forbid();
        var attachment = await db.TicketAttachments.SingleOrDefaultAsync(x => x.FileId == id, ct);
        if (attachment is not null)
            db.TicketAttachments.Remove(attachment);
        var evidence = await db.EvidenceFiles.SingleOrDefaultAsync(x => x.FileId == id, ct);
        if (evidence is not null)
            db.EvidenceFiles.Remove(evidence);
        var deleted = await storage.DeleteAsync(id, ct);
        db.AuditLogs.Add(
            new AuditLog
            {
                ActorUserId = principal.UserId(),
                Action = "FileDeleted",
                EntityType = nameof(FileObject),
                EntityId = id.ToString(),
            }
        );
        await db.SaveChangesAsync(ct);
        return deleted is null ? Results.NotFound() : Results.NoContent();
    }
}
