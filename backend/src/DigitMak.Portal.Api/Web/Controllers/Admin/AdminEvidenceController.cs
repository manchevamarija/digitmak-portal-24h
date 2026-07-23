using System.Text.Json;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;
using DigitMak.Portal.Api.Application.Exports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static DigitMak.Portal.Api.Web.Controllers.Admin.AdminSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "Admin")]
public sealed class AdminEvidenceController(PortalDbContext db, IFileStorage storage) : ControllerBase
{
    [HttpGet("evidence")]
    public async Task<IReadOnlyList<EvidenceFile>> GetEvidence(CancellationToken ct) =>
        await db.EvidenceFiles.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);

    [HttpGet("evidence-targets")]
    public async Task<IResult> GetTargets(string type, CancellationToken ct)
    {
        if (!EvidenceEntityTypes.Contains(type))
            return Results.BadRequest(new { message = "Unsupported evidence entity type." });

        List<EvidenceTargetOption> items = type switch
        {
            "Ticket" => await db
                .Tickets.OrderByDescending(x => x.CreatedAt)
                .Select(x => new EvidenceTargetOption(x.Id, x.TicketNumber + " · " + x.Title))
                .Take(100)
                .ToListAsync(ct),
            "Meeting" => await db
                .Meetings.OrderByDescending(x => x.CreatedAt)
                .Select(x => new EvidenceTargetOption(x.Id, x.Subject + " · " + x.Status))
                .Take(100)
                .ToListAsync(ct),
            "Subscription" => await db
                .Subscriptions.OrderByDescending(x => x.CreatedAt)
                .Select(x => new EvidenceTargetOption(x.Id, x.Status + " · " + x.UserId))
                .Take(100)
                .ToListAsync(ct),
            "ContactRequest" => await db
                .ContactRequests.OrderByDescending(x => x.CreatedAt)
                .Select(x => new EvidenceTargetOption(x.Id, x.OrganizationName + " · " + x.ContactName))
                .Take(100)
                .ToListAsync(ct),
            "KpiPeriod" => [new EvidenceTargetOption(Guid.NewGuid(), "Нов KPI извештаен период")],
            _ => [],
        };
        return Results.Ok(items);
    }

    [HttpGet("evidence-templates")]
    public async Task<IReadOnlyList<EvidenceTemplate>> GetTemplates(CancellationToken ct) =>
        await db.EvidenceTemplates.OrderBy(x => x.Code).ToListAsync(ct);

    [HttpPost("evidence-templates")]
    public async Task<IResult> CreateTemplate(EvidenceTemplateRequest request, CancellationToken ct)
    {
        var principal = User;
        if (
            string.IsNullOrWhiteSpace(request.Code)
            || string.IsNullOrWhiteSpace(request.Name)
            || !EvidenceEntityTypes.Contains(request.RelatedEntityType)
        )
            return Results.BadRequest(
                new { message = "Code, name and a supported related entity type are required." }
            );
        if (await db.EvidenceTemplates.AnyAsync(x => x.Code == request.Code.Trim(), ct))
            return Results.Conflict(new { message = "Evidence template code already exists." });
        var item = new EvidenceTemplate
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            RelatedEntityType = request.RelatedEntityType,
            Description = request.Description.Trim(),
            RequiredMetadataJson = JsonSerializer.Serialize(
                request
                    .RequiredMetadata.Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct()
            ),
            IsActive = request.IsActive,
        };
        db.EvidenceTemplates.Add(item);
        db.AuditLogs.Add(
            Audit(principal, "EvidenceTemplateCreated", nameof(EvidenceTemplate), item.Id)
        );
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/admin/evidence-templates/{item.Id}", item);
    }

    [HttpPut("evidence-templates/{id:guid}")]
    public async Task<IResult> UpdateTemplate(
        Guid id,
        EvidenceTemplateRequest request,
        CancellationToken ct
    )
    {
        var principal = User;
        var item = await db.EvidenceTemplates.FindAsync([id], ct);
        if (item is null)
            return Results.NotFound();
        if (!EvidenceEntityTypes.Contains(request.RelatedEntityType))
            return Results.BadRequest(new { message = "Unsupported related entity type." });
        item.Code = request.Code.Trim();
        item.Name = request.Name.Trim();
        item.RelatedEntityType = request.RelatedEntityType;
        item.Description = request.Description.Trim();
        item.RequiredMetadataJson = JsonSerializer.Serialize(
            request
                .RequiredMetadata.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct()
        );
        item.IsActive = request.IsActive;
        db.AuditLogs.Add(
            Audit(principal, "EvidenceTemplateUpdated", nameof(EvidenceTemplate), item.Id)
        );
        await db.SaveChangesAsync(ct);
        return Results.Ok(item);
    }

    [HttpGet("evidence-templates/{id:guid}/blank")]
    public async Task<IResult> BlankTemplate(Guid id, string? format, CancellationToken ct)
    {
        var item = await db.EvidenceTemplates.FindAsync([id], ct);
        if (item is null)
            return Results.NotFound();
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            return Results.File(
                SpreadsheetExports.EvidenceTemplateCsv(item),
                "text/csv; charset=utf-8",
                $"{item.Code}-template.csv"
            );
        return Results.File(
            SpreadsheetExports.EvidenceTemplate(item),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{item.Code}-образец.xlsx"
        );
    }

    [HttpPost("evidence")]
    public async Task<IResult> UploadEvidence(
        IFormFile file,
        string relatedEntityType,
        Guid relatedEntityId,
        string? kpiCategory,
        string? reportingPeriod,
        string? templateType,
        Guid? templateId,
        CancellationToken ct
    )
    {
        var principal = User;
        if (!EvidenceEntityTypes.Contains(relatedEntityType))
            return Results.BadRequest(new { message = "Unsupported evidence entity type." });
        if (!await EvidenceTargetExists(relatedEntityType, relatedEntityId, db, ct))
            return Results.NotFound(new { message = "The related KPI entity does not exist." });
        if (
            !string.IsNullOrWhiteSpace(reportingPeriod)
            && !System.Text.RegularExpressions.Regex.IsMatch(
                reportingPeriod,
                @"^\d{4}-(Q[1-4]|M(0[1-9]|1[0-2]))$"
            )
        )
            return Results.BadRequest(
                new { message = "Reporting period must use YYYY-Q1..Q4 or YYYY-M01..M12." }
            );
        var template = templateId is null
            ? null
            : await db.EvidenceTemplates.SingleOrDefaultAsync(
                x => x.Id == templateId && x.IsActive,
                ct
            );
        if (templateId is not null && template is null)
            return Results.BadRequest(new { message = "Evidence template was not found or is inactive." });
        if (template is not null && template.RelatedEntityType != relatedEntityType)
            return Results.BadRequest(
                new { message = "Evidence template does not match the related entity type." }
            );
        try
        {
            var stored = await storage.SaveAsync(
                file,
                "Evidence",
                relatedEntityId,
                principal.UserId(),
                ct
            );
            var item = new EvidenceFile
            {
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                FileId = stored.Id,
                KpiCategory = kpiCategory,
                ReportingPeriod = reportingPeriod,
                TemplateType = template?.Code ?? templateType,
                CreatedBy = principal.UserId(),
            };
            db.Add(item);
            db.AuditLogs.Add(Audit(principal, "EvidenceFileUploaded", nameof(EvidenceFile), item.Id));
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/admin/evidence/{item.Id}", item);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
