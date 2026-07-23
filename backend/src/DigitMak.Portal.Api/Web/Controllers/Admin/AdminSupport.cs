using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;

namespace DigitMak.Portal.Api.Web.Controllers.Admin;

/// <summary>
/// Helper methods shared by every admin controller. This mirrors the private
/// static helpers that used to live at the bottom of the old AdminEndpoints.cs
/// minimal-API module, moved here so multiple controller classes can reuse them
/// via <c>using static DigitMak.Portal.Api.Web.Controllers.Admin.AdminSupport;</c>.
/// </summary>
internal static class AdminSupport
{
    internal static readonly HashSet<string> EvidenceEntityTypes =
    [
        "Ticket",
        "Meeting",
        "Subscription",
        "ContactRequest",
        "KpiPeriod",
    ];

    internal static AuditLog Audit(
        ClaimsPrincipal principal,
        string action,
        string entityType,
        Guid id,
        string? oldValue = null,
        string? newValue = null
    ) =>
        new()
        {
            ActorUserId = principal.UserId(),
            Action = action,
            EntityType = entityType,
            EntityId = id.ToString(),
            OldValuesJson = oldValue,
            NewValuesJson = newValue,
        };

    internal static int Size(int? size) => Math.Clamp(size ?? 50, 1, 100);

    internal static int Offset(int? page, int? size) => (Math.Max(page ?? 1, 1) - 1) * Size(size);

    internal static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    internal static async Task<IResult> UpdateContact(
        Guid id,
        ClaimsPrincipal principal,
        PortalDbContext db,
        Action<ContactRequest> update,
        string audit
    )
    {
        var item = await db.ContactRequests.FindAsync(id);
        if (item is null)
            return Results.NotFound();
        update(item);
        db.AuditLogs.Add(Audit(principal, audit, nameof(ContactRequest), item.Id));
        await db.SaveChangesAsync();
        return Results.Ok(item);
    }

    internal static Task<bool> EvidenceTargetExists(
        string type,
        Guid id,
        PortalDbContext db,
        CancellationToken ct
    ) =>
        type switch
        {
            "Ticket" => db.Tickets.AnyAsync(item => item.Id == id, ct),
            "Meeting" => db.Meetings.AnyAsync(item => item.Id == id, ct),
            "Subscription" => db.Subscriptions.AnyAsync(item => item.Id == id, ct),
            "ContactRequest" => db.ContactRequests.AnyAsync(item => item.Id == id, ct),
            "KpiPeriod" => Task.FromResult(id != Guid.Empty),
            _ => Task.FromResult(false),
        };

    internal static async Task<object> ContentResult<T>(
        string type,
        DbSet<T> set,
        PortalDbContext db,
        CancellationToken ct
    )
        where T : Entity =>
        new
        {
            items = await set.OrderBy(x => x.CreatedAt).ToListAsync(ct),
            translations = await db.Translations.Where(x => x.EntityType == type).ToListAsync(ct),
        };

    internal static async Task SaveTranslations(
        Guid id,
        string type,
        Dictionary<string, Dictionary<string, string>> values,
        PortalDbContext db,
        CancellationToken ct
    )
    {
        foreach (var (language, fields) in values)
        foreach (var (field, value) in fields)
        {
            var item = await db.Translations.SingleOrDefaultAsync(
                x =>
                    x.EntityType == type
                    && x.EntityId == id
                    && x.Language == language
                    && x.FieldName == field,
                ct
            );
            if (item is null)
                db.Translations.Add(
                    new Translation
                    {
                        EntityType = type,
                        EntityId = id,
                        Language = language,
                        FieldName = field,
                        Value = value,
                    }
                );
            else
                item.Value = value;
        }
    }

    internal static IResult CsvFile(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        string filename
    )
    {
        var csv =
            string.Join(",", headers.Select(Csv))
            + "\r\n"
            + string.Join("\r\n", rows.Select(row => string.Join(",", row.Select(Csv))));
        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return Results.File(content, "text/csv; charset=utf-8", filename);
    }

    private static string Csv(string? value) =>
        '"' + (value ?? string.Empty).Replace("\"", "\"\"") + '"';

    internal sealed record EvidenceTargetOption(Guid Id, string Label);
}
