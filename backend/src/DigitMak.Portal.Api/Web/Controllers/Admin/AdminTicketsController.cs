using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitMak.Portal.Api.Web.Controllers.Admin;

[ApiController]
[Route("api/admin/tickets")]
[Authorize(Policy = "Admin")]
public sealed class AdminTicketsController(PortalDbContext db, ITicketService tickets) : ControllerBase
{
    [HttpPost]
    public async Task<IResult> Create(AdminTicketRequest request, CancellationToken ct)
    {
        var principal = User;
        var isClient = await db
            .UserRoles.Where(userRole => userRole.UserId == request.UserId)
            .Join(
                db.Roles.Where(role => role.Name == "Client"),
                userRole => userRole.RoleId,
                role => role.Id,
                (_, _) => true
            )
            .AnyAsync(ct);
        var isAdmin = await db
            .UserRoles.Where(userRole => userRole.UserId == request.UserId)
            .Join(
                db.Roles.Where(role => role.Name == "Admin"),
                userRole => userRole.RoleId,
                role => role.Id,
                (_, _) => true
            )
            .AnyAsync(ct);
        if (!isClient || isAdmin)
            return Results.BadRequest(new { message = "Select a client account, not an administrator." });
        var item = await tickets.CreateForUserAsync(
            new TicketRequest(request.Category, request.Title, request.Description, request.Priority),
            request.UserId,
            principal.UserId(),
            request.OrganizationId,
            ct
        );
        return item is not null
            ? Results.Created($"/api/tickets/{item.Id}", item)
            : Results.Json(
                new
                {
                    code = "CLIENT_TICKET_ACCESS_REQUIRED",
                    message = "The selected client needs an approved organization and an active subscription.",
                },
                statusCode: StatusCodes.Status409Conflict
            );
    }
}
