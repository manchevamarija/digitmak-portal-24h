using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;
using static DigitMak.Portal.Api.Web.Controllers.Admin.AdminSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "Admin")]
public sealed class AdminContactRequestsController(PortalDbContext db, IConfiguration config)
    : ControllerBase
{
    [HttpGet("contact-requests")]
    public async Task<IReadOnlyList<ContactRequest>> Get(
        int? page,
        int? pageSize,
        string? status,
        CancellationToken ct
    ) =>
        await db
            .ContactRequests.Where(x => status == null || x.Status == status)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(Offset(page, pageSize))
            .Take(Size(pageSize))
            .ToListAsync(ct);

    [HttpGet("ticket-attachments")]
    public async Task<object> GetTicketAttachments(CancellationToken ct) =>
        await db
            .TicketAttachments.Join(
                db.Files,
                attachment => attachment.FileId,
                file => file.Id,
                (attachment, file) => new { attachment, file }
            )
            .Join(
                db.Tickets,
                item => item.attachment.TicketId,
                ticket => ticket.Id,
                (item, ticket) => new { item.attachment, item.file, ticket }
            )
            .Join(
                db.Organizations,
                item => item.ticket.OrganizationId,
                organization => organization.Id,
                (item, organization) =>
                    new
                    {
                        item.attachment.Id,
                        item.attachment.TicketId,
                        item.attachment.MessageId,
                        item.attachment.FileId,
                        item.attachment.UploadedBy,
                        item.attachment.CreatedAt,
                        item.file.OriginalFilename,
                        item.file.ContentType,
                        item.file.SizeBytes,
                        item.file.Checksum,
                        item.ticket.TicketNumber,
                        TicketTitle = item.ticket.Title,
                        OrganizationName = organization.Name,
                    }
            )
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    [HttpGet("contact-requests/{id:guid}")]
    public async Task<IResult> GetOne(Guid id) =>
        await db.ContactRequests.FindAsync(id) is { } item ? Results.Ok(item) : Results.NotFound();

    [HttpPatch("contact-requests/{id:guid}")]
    public async Task<IResult> Update(Guid id, ContactUpdateRequest request)
    {
        var principal = User;
        var item = await db.ContactRequests.FindAsync(id);
        if (item is null)
            return Results.NotFound();
        if (
            request.DmaCategory is not null
            && !DmaCategoryMapping.IsValid(request.DmaCategory)
        )
            return Results.BadRequest(new { message = "Unsupported internal DMA category." });
        item.Status = request.Status;
        item.AssignedTo = request.AssignedTo;
        item.LinkedOrganizationId = request.LinkedOrganizationId;
        item.DmaCategory = request.DmaCategory ?? item.DmaCategory;
        db.AuditLogs.Add(Audit(principal, "ContactRequestUpdated", nameof(ContactRequest), item.Id));
        await db.SaveChangesAsync();
        return Results.Ok(item);
    }

    [HttpPost("contact-requests/{id:guid}/assign")]
    public async Task<IResult> Assign(Guid id, Guid userId)
    {
        var item = await db.ContactRequests.FindAsync(id);
        if (item is null)
            return Results.NotFound();

        item.AssignedTo = userId;
        item.Status = "Assigned";
        db.Notifications.Add(StatusEmail(item, "Assigned"));
        db.AuditLogs.Add(Audit(User, "ContactRequestAssigned", nameof(ContactRequest), item.Id));
        await db.SaveChangesAsync();
        return Results.Ok(item);
    }

    [HttpPost("contact-requests/{id:guid}/mark-handled")]
    public async Task<IResult> MarkHandled(Guid id)
    {
        var item = await db.ContactRequests.FindAsync(id);
        if (item is null)
            return Results.NotFound();

        item.Status = "Handled";
        db.Notifications.Add(StatusEmail(item, "Handled"));
        db.AuditLogs.Add(Audit(User, "ContactRequestHandled", nameof(ContactRequest), item.Id));
        await db.SaveChangesAsync();
        return Results.Ok(item);
    }

    [HttpPost("contact-requests/{id:guid}/link-organization")]
    public Task<IResult> LinkOrganization(Guid id, Guid organizationId)
    {
        var principal = User;
        return UpdateContact(
            id,
            principal,
            db,
            item => item.LinkedOrganizationId = organizationId,
            "ContactRequestLinked"
        );
    }

    [HttpPost("contact-requests/{id:guid}/respond")]
    public async Task<IResult> Respond(Guid id, MessageRequest request)
    {
        var principal = User;
        var item = await db.ContactRequests.FindAsync(id);
        if (item is null)
            return Results.NotFound();
        db.Notifications.Add(
            new Notification
            {
                RecipientEmail = item.Email,
                Language = item.PreferredLanguage,
                Type = "ContactRequestResponse",
                Subject = ResponseSubject(item.PreferredLanguage),
                Body = ResponseBody(item.PreferredLanguage, request.Body),
            }
        );
        item.Status = "Responded";
        db.AuditLogs.Add(Audit(principal, "ContactRequestResponded", nameof(ContactRequest), item.Id));
        await db.SaveChangesAsync();
        return Results.Accepted();
    }

    [HttpPost("contact-requests/{id:guid}/invite-registration")]
    public async Task<IResult> InviteRegistration(Guid id)
    {
        var principal = User;
        var item = await db.ContactRequests.FindAsync(id);
        if (item is null)
            return Results.NotFound();
        var root = (config["APP_PUBLIC_URL"] ?? "http://localhost:5173").TrimEnd('/');
        db.Notifications.Add(
            new Notification
            {
                RecipientEmail = item.Email,
                Language = item.PreferredLanguage,
                Type = "RegistrationInvitation",
                Subject = "Join DigitMak Portal",
                Body = $"<p><a href=\"{root}/register\">Create your DigitMak account</a></p>",
            }
        );
        item.Status = "RegistrationInvited";
        db.AuditLogs.Add(
            Audit(principal, "ContactRegistrationInvited", nameof(ContactRequest), item.Id)
        );
        await db.SaveChangesAsync();
        return Results.Accepted();
    }
    private static Notification StatusEmail(ContactRequest item, string status)
    {
        var language = item.PreferredLanguage?.ToLowerInvariant() ?? "mk";
        var isHandled = status == "Handled";
        var subject = language switch
        {
            "en" => isHandled
                ? "Your DigitMak contact request has been processed"
                : "Your DigitMak contact request is being processed",
            "sq" => isHandled
                ? "Kërkesa juaj e kontaktit në DigitMak është përpunuar"
                : "Kërkesa juaj e kontaktit në DigitMak është në përpunim",
            _ => isHandled
                ? "Вашето контакт барање до DigitMak е обработено"
                : "Вашето контакт барање до DigitMak е во обработка",
        };
        var body = language switch
        {
            "en" => isHandled
                ? "<p>Status: <strong>Processed</strong>.</p><p>Your contact request has been completed. If an additional answer or next step is needed, our team will send it to this email address.</p>"
                : "<p>Status: <strong>In processing</strong>.</p><p>A member of the DigitMak team has started reviewing your contact request.</p>",
            "sq" => isHandled
                ? "<p>Statusi: <strong>E përpunuar</strong>.</p><p>Kërkesa juaj e kontaktit është përfunduar. Nëse nevojitet përgjigje ose hap tjetër, ekipi ynë do ta dërgojë në këtë adresë emaili.</p>"
                : "<p>Statusi: <strong>Në përpunim</strong>.</p><p>Një anëtar i ekipit DigitMak ka filluar shqyrtimin e kërkesës suaj.</p>",
            _ => isHandled
                ? "<p>Статус: <strong>Обработено</strong>.</p><p>Вашето контакт барање е завршено. Доколку има дополнителен одговор или следен чекор, нашиот тим ќе го испрати на оваа е-пошта.</p>"
                : "<p>Статус: <strong>Во обработка</strong>.</p><p>Член од тимот на DigitMak започна со разгледување на вашето контакт барање.</p>",
        };
        return new Notification
        {
            RecipientEmail = item.Email,
            Language = language,
            Type = $"ContactRequest{status}",
            Subject = subject,
            Body = body,
        };
    }

    private static string ResponseSubject(string? language) =>
        language?.ToLowerInvariant() switch
        {
            "en" => "DigitMak response to your contact request",
            "sq" => "Përgjigje nga DigitMak për kërkesën tuaj",
            _ => "Одговор од DigitMak за вашето контакт барање",
        };

    private static string ResponseBody(string? language, string response)
    {
        var encoded = System.Net.WebUtility.HtmlEncode(response);
        var status = language?.ToLowerInvariant() switch
        {
            "en" => "<p>Status: <strong>Processed – response sent</strong>.</p>",
            "sq" => "<p>Statusi: <strong>E përpunuar – përgjigjja u dërgua</strong>.</p>",
            _ => "<p>Статус: <strong>Обработено – испратен одговор</strong>.</p>",
        };
        return $"{status}<p>{encoded}</p>";
    }

}
