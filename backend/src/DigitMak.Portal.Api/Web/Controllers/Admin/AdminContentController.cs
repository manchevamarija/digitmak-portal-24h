using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using static DigitMak.Portal.Api.Web.Controllers.Admin.AdminSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "Admin")]
public sealed class AdminContentController(PortalDbContext db) : ControllerBase
{
    private static readonly string[] SupportedLanguages = ["mk", "en", "sq"];
    private static readonly string[] SupportedStatuses = ["Published", "Draft", "Archived"];

    [HttpGet("services")]
    public async Task<object> GetServices(CancellationToken ct) =>
        await ContentResult("ServiceCatalogueItem", db.ServiceCatalogueItems, db, ct);

    [HttpPost("services")]
    public async Task<IResult> UpsertService(ContentUpsertRequest request, CancellationToken ct)
    {
        if (Validate(request) is { } error)
            return Results.BadRequest(new { message = error });

        var principal = User;
        var slug = request.Slug.Trim().ToLowerInvariant();
        var item =
            await db.ServiceCatalogueItems.SingleOrDefaultAsync(x => x.Slug == slug, ct)
            ?? new ServiceCatalogueItem { Slug = slug };
        item.Status = request.Status.Trim();
        item.Category = request.Category.Trim();
        if (db.Entry(item).State == EntityState.Detached)
            db.Add(item);
        await SaveTranslations(
            item.Id,
            nameof(ServiceCatalogueItem),
            NormalizeTranslations(request.Translations),
            db,
            ct
        );
        db.AuditLogs.Add(
            Audit(principal, "ServiceCatalogueChanged", nameof(ServiceCatalogueItem), item.Id)
        );
        await db.SaveChangesAsync(ct);
        return Results.Ok(item);
    }

    [HttpGet("pages")]
    public async Task<object> GetPages(CancellationToken ct) =>
        await ContentResult("ContentPage", db.ContentPages, db, ct);

    [HttpPost("pages")]
    public async Task<IResult> UpsertPage(ContentUpsertRequest request, CancellationToken ct)
    {
        if (Validate(request) is { } error)
            return Results.BadRequest(new { message = error });

        var principal = User;
        var slug = request.Slug.Trim().ToLowerInvariant();
        var item =
            await db.ContentPages.SingleOrDefaultAsync(x => x.Slug == slug, ct)
            ?? new ContentPage { Slug = slug };
        item.Status = request.Status.Trim();
        if (db.Entry(item).State == EntityState.Detached)
            db.Add(item);
        await SaveTranslations(
            item.Id,
            nameof(ContentPage),
            NormalizeTranslations(request.Translations),
            db,
            ct
        );
        db.AuditLogs.Add(Audit(principal, "ContentPageChanged", nameof(ContentPage), item.Id));
        await db.SaveChangesAsync(ct);
        return Results.Ok(item);
    }

    private static string? Validate(ContentUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Slug))
            return "A URL slug is required.";
        if (!Regex.IsMatch(request.Slug.Trim(), "^[a-z0-9]+(?:-[a-z0-9]+)*$"))
            return "The URL slug may contain only lowercase letters, numbers, and single hyphens.";
        if (!SupportedStatuses.Contains(request.Status, StringComparer.Ordinal))
            return "Unsupported content status.";
        if (string.IsNullOrWhiteSpace(request.Category))
            return "A category is required.";
        if (request.Translations is null || FindSourceTranslation(request.Translations) is null)
            return "A title and description are required.";

        return null;
    }

    private static KeyValuePair<string, Dictionary<string, string>>? FindSourceTranslation(
        Dictionary<string, Dictionary<string, string>> translations
    )
    {
        foreach (var language in SupportedLanguages)
        {
            if (!translations.TryGetValue(language, out var fields) || fields is null)
                continue;
            if (!fields.TryGetValue("title", out var title) || string.IsNullOrWhiteSpace(title))
                continue;
            if (
                !fields.TryGetValue("description", out var description)
                || string.IsNullOrWhiteSpace(description)
            )
                continue;

            return new KeyValuePair<string, Dictionary<string, string>>(language, fields);
        }

        return null;
    }

    private static Dictionary<string, Dictionary<string, string>> NormalizeTranslations(
        Dictionary<string, Dictionary<string, string>> translations
    )
    {
        var source = FindSourceTranslation(translations)!.Value.Value;
        var title = source["title"].Trim();
        var description = source["description"].Trim();

        return SupportedLanguages.ToDictionary(
            language => language,
            _ => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = title,
                ["description"] = description,
            },
            StringComparer.Ordinal
        );
    }
}
