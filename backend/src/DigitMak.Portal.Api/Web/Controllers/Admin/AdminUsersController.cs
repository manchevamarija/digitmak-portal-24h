using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using static DigitMak.Portal.Api.Web.Controllers.Admin.AdminSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "Admin")]
public sealed class AdminUsersController(PortalDbContext db, UserManager<AppUser> users, RoleManager<IdentityRole<Guid>> roles)
    : ControllerBase
{
    [HttpGet]
    public async Task<object> Get(CancellationToken ct)
    {
        var items = await db
            .Users.Select(x => new
            {
                x.Id,
                x.Email,
                x.FirstName,
                x.LastName,
                x.PhoneNumber,
                x.PreferredLanguage,
                x.Status,
                x.EmailVerifiedAt,
                x.OrganizationId,
                x.CreatedAt,
                x.UpdatedAt,
            })
            .ToListAsync(ct);
        var roleRows = await db
            .UserRoles.Join(
                db.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, Role = role.Name! }
            )
            .ToListAsync(ct);
        return items.Select(item => new
        {
            item.Id,
            item.Email,
            item.FirstName,
            item.LastName,
            item.PhoneNumber,
            item.PreferredLanguage,
            item.Status,
            item.EmailVerifiedAt,
            item.OrganizationId,
            item.CreatedAt,
            item.UpdatedAt,
            Roles = roleRows
                .Where(role => role.UserId == item.Id)
                .Select(role => role.Role)
                .OrderBy(role => role)
                .ToArray(),
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> GetOne(Guid id) =>
        await users.FindByIdAsync(id.ToString()) is { } user
            ? Results.Ok(
                new
                {
                    user.Id,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.PhoneNumber,
                    user.PreferredLanguage,
                    user.Status,
                    user.EmailVerifiedAt,
                    user.OrganizationId,
                    user.CreatedAt,
                    user.UpdatedAt,
                    roles = await users.GetRolesAsync(user),
                }
            )
            : Results.NotFound();

    [HttpPatch("{id:guid}")]
    public async Task<IResult> Update(Guid id, UserUpdateRequest request)
    {
        var principal = User;
        var user = await db.Users.FindAsync(id);
        if (user is null)
            return Results.NotFound();
        var previousStatus = user.Status;
        var nextStatus = request.Status;
        if (!UserStatuses.IsValid(nextStatus))
            return Results.BadRequest(new { message = "Unsupported user status." });
        user.Status = nextStatus;
        user.PreferredLanguage = request.PreferredLanguage;
        user.PhoneNumber = request.Phone;
        if (previousStatus == UserStatuses.Active && nextStatus != UserStatuses.Active)
        {
            var now = DateTimeOffset.UtcNow;
            var tokens = await db
                .RefreshTokens.Where(token => token.UserId == id && token.RevokedAt == null)
                .ToListAsync();
            foreach (var token in tokens)
                token.RevokedAt = now;
            user.SecurityStamp = Guid.NewGuid().ToString();
        }
        db.AuditLogs.Add(
            Audit(principal, "UserStatusChanged", nameof(AppUser), id, previousStatus, nextStatus)
        );
        await db.SaveChangesAsync();
        return Results.Ok(user);
    }

    [HttpPost("{id:guid}/roles")]
    public async Task<IResult> AddRoles(Guid id, RolesRequest request)
    {
        var principal = User;
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
            return Results.NotFound();
        foreach (var role in request.Roles.Distinct())
        {
            if (!await roles.RoleExistsAsync(role))
                return Results.BadRequest(new { message = $"Unknown role {role}." });
            if (!await users.IsInRoleAsync(user, role))
                await users.AddToRoleAsync(user, role);
        }
        db.AuditLogs.Add(
            Audit(
                principal,
                "RoleAdded",
                nameof(AppUser),
                id,
                null,
                JsonSerializer.Serialize(request.Roles)
            )
        );
        await db.SaveChangesAsync();
        return Results.Ok(await users.GetRolesAsync(user));
    }

    [HttpDelete("{id:guid}/roles/{role}")]
    public async Task<IResult> RemoveRole(Guid id, string role)
    {
        var principal = User;
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
            return Results.NotFound();
        await users.RemoveFromRoleAsync(user, role);
        db.AuditLogs.Add(Audit(principal, "RoleRemoved", nameof(AppUser), id, role));
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
