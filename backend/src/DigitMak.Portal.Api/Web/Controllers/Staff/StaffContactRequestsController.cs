using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;

namespace DigitMak.Portal.Api.Web.Controllers.Staff;

[ApiController]
[Route("api/staff/contact-requests")]
[Authorize(Policy = "Staff")]
public sealed class StaffContactRequestsController(PortalDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IResult> Get(int? page, int? pageSize, CancellationToken ct)
    {
        var principal = User;
        if (!principal.IsInRole("Admin") && !principal.IsInRole("HelpDeskAgent"))
            return Results.Forbid();
        var items = await db
            .ContactRequests.OrderByDescending(x => x.CreatedAt)
            .Skip((Math.Max(page ?? 1, 1) - 1) * Math.Clamp(pageSize ?? 50, 1, 100))
            .Take(Math.Clamp(pageSize ?? 50, 1, 100))
            .ToListAsync(ct);
        return Results.Ok(items);
    }
}
