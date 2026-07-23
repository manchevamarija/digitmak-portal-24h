using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using static DigitMak.Portal.Api.Web.Controllers.Admin.AdminSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Admin;

[ApiController]
[Route("api/admin/settings")]
[Authorize(Policy = "Admin")]
public sealed class AdminSettingsController(PortalDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<SystemSetting>> Get(CancellationToken ct) =>
        await db.SystemSettings.OrderBy(x => x.Key).ToListAsync(ct);

    [HttpPut("{key}")]
    public async Task<IResult> Upsert(string key, SettingRequest request, CancellationToken ct)
    {
        var principal = User;
        var item =
            await db.SystemSettings.SingleOrDefaultAsync(x => x.Key == key, ct)
            ?? new SystemSetting { Key = key };
        item.Value = request.Value;
        item.Description = request.Description;
        if (db.Entry(item).State == EntityState.Detached)
            db.Add(item);
        db.AuditLogs.Add(Audit(principal, "SystemSettingChanged", nameof(SystemSetting), item.Id));
        await db.SaveChangesAsync(ct);
        return Results.Ok(item);
    }
}
