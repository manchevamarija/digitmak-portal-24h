using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;
using DigitMak.Portal.Api.Application.Exports;
using static DigitMak.Portal.Api.Web.Controllers.Admin.AdminSupport;

namespace DigitMak.Portal.Api.Web.Controllers.Admin;

[ApiController]
[Route("api/admin/reports")]
[Authorize(Policy = "Admin")]
public sealed class AdminReportsController(PortalDbContext db) : ControllerBase
{
    [HttpGet("kpis")]
    public async Task<IResult> Kpis(CancellationToken ct)
    {
        var clientRoleUserIds = db.UserRoles.Join(
            db.Roles.Where(role => role.Name == "Client"),
            userRole => userRole.RoleId,
            role => role.Id,
            (userRole, _) => userRole.UserId
        );
        var privilegedUserIds = db.UserRoles.Join(
            db.Roles.Where(role => role.Name == "Admin"),
            userRole => userRole.RoleId,
            role => role.Id,
            (userRole, _) => userRole.UserId
        );
        var clientUserIds = clientRoleUserIds.Where(userId => !privilegedUserIds.Contains(userId));
        return Results.Ok(
            new
            {
                activeSubscriptions = await db.Subscriptions.CountAsync(
                    x => x.Status == "Active" && clientUserIds.Contains(x.UserId),
                    ct
                ),
                expiredSubscriptions = await db.Subscriptions.CountAsync(
                    x =>
                        (x.Status == "Expired" || x.Status == "Cancelled")
                        && clientUserIds.Contains(x.UserId),
                    ct
                ),
                aiHelpDeskSubscriptions = await db.Subscriptions.CountAsync(
                    x => clientUserIds.Contains(x.UserId),
                    ct
                ),
                tickets = await db.Tickets.CountAsync(ct),
                newTickets = await db.Tickets.CountAsync(x => x.Status == "New", ct),
                meetings = await db.Meetings.CountAsync(ct),
                confirmedMeetings = await db.Meetings.CountAsync(x => x.Status == "Confirmed", ct),
                contactRequests = await db.ContactRequests.CountAsync(ct),
                publicInstitutions = await db.Organizations.CountAsync(
                    x => x.Type == "PublicInstitution" && x.Status == "Approved",
                    ct
                ),
                aiActRequests = await db.Tickets.CountAsync(x => x.Category == "AI_ACT_COMPLIANCE", ct),
                referrals = await db.Tickets.CountAsync(x => x.ReferralRecommendation != null, ct),
            }
        );
    }

    [HttpGet("subscriptions")]
    public async Task<object> Subscriptions(CancellationToken ct)
    {
        var clientRoleUserIds = db.UserRoles.Join(
            db.Roles.Where(role => role.Name == "Client"),
            userRole => userRole.RoleId,
            role => role.Id,
            (userRole, _) => userRole.UserId
        );
        var privilegedUserIds = db.UserRoles.Join(
            db.Roles.Where(role => role.Name == "Admin"),
            userRole => userRole.RoleId,
            role => role.Id,
            (userRole, _) => userRole.UserId
        );
        var clientUserIds = clientRoleUserIds.Where(userId => !privilegedUserIds.Contains(userId));
        return await db
            .Subscriptions.Where(item => clientUserIds.Contains(item.UserId))
            .GroupBy(x => x.Status)
            .Select(x => new { status = x.Key, count = x.Count() })
            .ToListAsync(ct);
    }

    [HttpGet("tickets")]
    public async Task<object> Tickets(CancellationToken ct) =>
        await db
            .Tickets.GroupBy(x => new { x.Status, x.Category })
            .Select(x => new
            {
                x.Key.Status,
                x.Key.Category,
                count = x.Count(),
            })
            .ToListAsync(ct);

    [HttpGet("contacts")]
    public async Task<IResult> Contacts(CancellationToken ct) =>
        Results.Ok(
            new
            {
                byOrganizationType = await db
                    .ContactRequests.GroupBy(x => x.OrganizationType)
                    .Select(x => new { key = x.Key, count = x.Count() })
                    .OrderByDescending(x => x.count)
                    .ToListAsync(ct),
                bySector = await db
                    .ContactRequests.GroupBy(x => x.Sector ?? "Unspecified")
                    .Select(x => new { key = x.Key, count = x.Count() })
                    .OrderByDescending(x => x.count)
                    .ToListAsync(ct),
                byRegion = await db
                    .ContactRequests.GroupBy(x => x.Region ?? "Unspecified")
                    .Select(x => new { key = x.Key, count = x.Count() })
                    .OrderByDescending(x => x.count)
                    .ToListAsync(ct),
                byNeed = await db
                    .ContactRequests.GroupBy(x => x.MainNeed)
                    .Select(x => new { key = x.Key, count = x.Count() })
                    .OrderByDescending(x => x.count)
                    .ToListAsync(ct),
                byDmaCategory = await db
                    .ContactRequests.GroupBy(x => x.DmaCategory)
                    .Select(x => new { key = x.Key, count = x.Count() })
                    .OrderByDescending(x => x.count)
                    .ToListAsync(ct),
            }
        );

    [HttpGet("tickets-detailed")]
    public async Task<IResult> TicketsDetailed(CancellationToken ct) =>
        Results.Ok(
            new
            {
                byCategory = await db
                    .Tickets.GroupBy(x => x.Category)
                    .Select(x => new { key = x.Key, count = x.Count() })
                    .OrderByDescending(x => x.count)
                    .ToListAsync(ct),
                byStatus = await db
                    .Tickets.GroupBy(x => x.Status)
                    .Select(x => new { key = x.Key, count = x.Count() })
                    .OrderByDescending(x => x.count)
                    .ToListAsync(ct),
                byAssignee = await db
                    .Tickets.GroupBy(x => x.AssignedAgentId)
                    .Select(x => new
                    {
                        key = x.Key == null ? "Unassigned" : x.Key.ToString(),
                        count = x.Count(),
                    })
                    .OrderByDescending(x => x.count)
                    .ToListAsync(ct),
                byOrganizationType = await db
                    .Tickets.Join(
                        db.Organizations,
                        ticket => ticket.OrganizationId,
                        organization => organization.Id,
                        (ticket, organization) => organization.Type
                    )
                    .GroupBy(x => x)
                    .Select(x => new { key = x.Key, count = x.Count() })
                    .OrderByDescending(x => x.count)
                    .ToListAsync(ct),
            }
        );

    [HttpGet("meetings")]
    public async Task<IResult> Meetings(CancellationToken ct) =>
        Results.Ok(
            new
            {
                byStatus = await db
                    .Meetings.GroupBy(x => x.Status)
                    .Select(x => new { key = x.Key, count = x.Count() })
                    .OrderByDescending(x => x.count)
                    .ToListAsync(ct),
                byType = await db
                    .Meetings.GroupBy(x => x.MeetingType)
                    .Select(x => new { key = x.Key, count = x.Count() })
                    .OrderByDescending(x => x.count)
                    .ToListAsync(ct),
            }
        );

    [HttpGet("referrals")]
    public async Task<object> Referrals(CancellationToken ct) =>
        await db
            .Tickets.Where(x => x.ReferralRecommendation != null && x.ReferralRecommendation != "")
            .GroupBy(x => x.ReferralRecommendation!)
            .Select(x => new { key = x.Key, count = x.Count() })
            .OrderByDescending(x => x.count)
            .ToListAsync(ct);

    [HttpGet("export")]
    public async Task<IResult> Export(CancellationToken ct)
    {
        var rows = await db.Tickets.OrderBy(x => x.CreatedAt).ToListAsync(ct);
        return CsvFile(
            [
                "Број на тикет",
                "Категорија",
                "Приоритет",
                "Статус",
                "Организација ID",
                "Одговорно лице ID",
                "Креиран",
            ],
            rows.Select(x =>
                    (IReadOnlyList<string?>)
                        [
                            x.TicketNumber,
                            x.Category,
                            x.Priority,
                            x.Status,
                            x.OrganizationId.ToString(),
                            x.AssignedAgentId?.ToString(),
                            x.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                        ]
                )
                .ToList(),
            "digitmak-tickets.csv"
        );
    }

    [HttpGet("export/{dataset}")]
    public async Task<IResult> ExportDataset(string dataset, string? format, CancellationToken ct)
    {
        string title;
        IReadOnlyList<string> headers;
        IReadOnlyList<IReadOnlyList<string?>> rows;
        switch (dataset.ToLowerInvariant())
        {
            case "contacts":
                title = "DigitMak · Контакт-барања";
                headers =
                [
                    "Организација",
                    "Тип",
                    "Сектор",
                    "Регион",
                    "DMA категорија",
                    "Главна потреба",
                    "Статус",
                    "Креирано",
                ];
                rows = (await db.ContactRequests.OrderBy(x => x.CreatedAt).ToListAsync(ct))
                    .Select(x =>
                        (IReadOnlyList<string?>)
                            [
                                x.OrganizationName,
                                x.OrganizationType,
                                x.Sector,
                                x.Region,
                                x.DmaCategory,
                                x.MainNeed,
                                x.Status,
                                x.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                            ]
                    )
                    .ToList();
                break;
            case "meetings":
                title = "DigitMak · Состаноци";
                headers =
                [
                    "Наслов",
                    "Тип",
                    "Статус",
                    "Организација ID",
                    "Одговорно лице ID",
                    "Почеток",
                    "Креирано",
                ];
                rows = (await db.Meetings.OrderBy(x => x.CreatedAt).ToListAsync(ct))
                    .Select(x =>
                        (IReadOnlyList<string?>)
                            [
                                x.Subject,
                                x.MeetingType,
                                x.Status,
                                x.OrganizationId.ToString(),
                                x.AssignedUserId?.ToString(),
                                x.StartsAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                                x.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                            ]
                    )
                    .ToList();
                break;
            case "subscriptions":
                title = "DigitMak · Претплати";
                headers = ["Корисник ID", "Организација ID", "Статус", "Почеток", "Истекува", "Креирано"];
                rows = (await db.Subscriptions.OrderBy(x => x.CreatedAt).ToListAsync(ct))
                    .Select(x =>
                        (IReadOnlyList<string?>)
                            [
                                x.UserId.ToString(),
                                x.OrganizationId.ToString(),
                                x.Status,
                                x.StartsAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                                x.ExpiresAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                                x.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                            ]
                    )
                    .ToList();
                break;
            case "tickets":
                title = "DigitMak · Тикети";
                headers =
                [
                    "Број на тикет",
                    "Категорија",
                    "Приоритет",
                    "Статус",
                    "Организација ID",
                    "Одговорно лице ID",
                    "Креиран",
                ];
                rows = (await db.Tickets.OrderBy(x => x.CreatedAt).ToListAsync(ct))
                    .Select(x =>
                        (IReadOnlyList<string?>)
                            [
                                x.TicketNumber,
                                x.Category,
                                x.Priority,
                                x.Status,
                                x.OrganizationId.ToString(),
                                x.AssignedAgentId?.ToString(),
                                x.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                            ]
                    )
                    .ToList();
                break;
            default:
                return Results.BadRequest(
                    new { message = "Dataset must be tickets, contacts, meetings or subscriptions." }
                );
        }
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            return CsvFile(headers, rows, $"digitmak-{dataset.ToLowerInvariant()}.csv");

        var workbook = SpreadsheetExports.Report(title, headers, rows);
        return Results.File(
            workbook,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"digitmak-{dataset.ToLowerInvariant()}.xlsx"
        );
    }
}
