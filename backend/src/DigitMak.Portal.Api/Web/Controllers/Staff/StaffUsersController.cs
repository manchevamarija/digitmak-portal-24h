using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;

namespace DigitMak.Portal.Api.Web.Controllers.Staff;

[ApiController]
[Route("api/staff/users")]
[Authorize(Policy = "Staff")]
public sealed class StaffUsersController(PortalDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IResult> Get(CancellationToken ct)
    {
        var principal = User;
        if (!principal.IsInRole("Admin") && !principal.IsInRole("HelpDeskAgent"))
            return Results.Forbid();
        var items = await db
            .UserRoles.Join(
                db.Roles,
                ur => ur.RoleId,
                role => role.Id,
                (ur, role) => new { ur.UserId, Role = role.Name! }
            )
            .Join(
                db.Users,
                x => x.UserId,
                user => user.Id,
                (x, user) =>
                    new
                    {
                        user.Id,
                        user.Email,
                        user.FirstName,
                        user.LastName,
                        x.Role,
                    }
            )
            .Where(x => x.Role == "HelpDeskAgent" || x.Role == "Expert" || x.Role == "Admin")
            .OrderBy(x => x.Email)
            .ToListAsync(ct);
        return Results.Ok(items);
    }
}
