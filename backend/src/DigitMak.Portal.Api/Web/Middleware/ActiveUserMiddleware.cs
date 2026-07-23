using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;

namespace DigitMak.Portal.Api.Web.Middleware;

/// <summary>
/// Re-checks the persisted account state and roles after JWT validation so an administrator can
/// revoke an already issued access token immediately by deactivating the account or changing roles.
/// </summary>
public sealed class ActiveUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, PortalDbContext db)
    {
        if (
            context.User.Identity?.IsAuthenticated == true
            && TryGetUserId(context.User, out var userId)
        )
        {
            var state = await db
                .Users.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new
                {
                    user.Status,
                    user.EmailConfirmed,
                    user.EmailVerifiedAt,
                })
                .SingleOrDefaultAsync(context.RequestAborted);

            if (
                state is null
                || state.Status != UserStatuses.Active
                || !state.EmailConfirmed
                || state.EmailVerifiedAt is null
            )
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new { message = "This account is inactive or no longer verified." },
                    context.RequestAborted
                );
                return;
            }

            var persistedRoles = await db
                .UserRoles.Where(userRole => userRole.UserId == userId)
                .Join(
                    db.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (_, role) => role.Name!
                )
                .ToListAsync(context.RequestAborted);
            var tokenRoles = context
                .User.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.Ordinal);
            if (!tokenRoles.SetEquals(persistedRoles))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new { message = "Account permissions changed. Please sign in again." },
                    context.RequestAborted
                );
                return;
            }
        }

        await next(context);
    }

    internal static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
