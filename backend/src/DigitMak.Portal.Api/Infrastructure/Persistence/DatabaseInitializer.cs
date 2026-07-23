using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DigitMak.Portal.Api.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializePortalAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        if (
            db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true
        )
            await db.Database.MigrateAsync();
        else
        {
            await db.Database.EnsureCreatedAsync();
            await EnsureLocalSchemaAsync(db);
        }
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { "Admin", "HelpDeskAgent", "Expert", "Client" })
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole<Guid>(role));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email =
            Environment.GetEnvironmentVariable("ADMIN_BOOTSTRAP_EMAIL") ?? "admin@digitmak.mk";
        var admin = await users.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                EmailVerifiedAt = DateTimeOffset.UtcNow,
                Status = UserStatuses.Active,
                FirstName = "Portal",
                LastName = "Admin",
                TermsAcceptedAt = DateTimeOffset.UtcNow,
            };
            var created = await users.CreateAsync(
                admin,
                Environment.GetEnvironmentVariable("ADMIN_BOOTSTRAP_PASSWORD")
                    ?? "DigitMak!2026Admin"
            );
            if (!created.Succeeded)
                throw new InvalidOperationException(
                    "The bootstrap administrator could not be created: "
                        + string.Join(", ", created.Errors.Select(x => x.Description))
                );
            var roleAdded = await users.AddToRoleAsync(admin, "Admin");
            if (!roleAdded.Succeeded)
                throw new InvalidOperationException(
                    "The administrator role could not be assigned: "
                        + string.Join(", ", roleAdded.Errors.Select(x => x.Description))
                );
        }
        if (configuration.GetValue<bool>("DemoAccount:Enabled") || environment.IsDevelopment())
            await EnsureDemoClientAsync(users, db, admin, configuration);
        if (!await db.SystemSettings.AnyAsync(x => x.Key == "DataRetentionDays"))
            db.SystemSettings.Add(
                new SystemSetting
                {
                    Key = "DataRetentionDays",
                    Value = "730",
                    Description = "Retention for operational notifications and temporary records.",
                }
            );
        var evidenceTemplates = new[]
        {
            Template(
                "TICKET-RESOLUTION",
                "Ticket resolution evidence",
                "Ticket",
                "Evidence of delivered help-desk guidance and resolution.",
                "ticketNumber",
                "category",
                "finalRecommendation",
                "resolvedAt"
            ),
            Template(
                "MEETING-DELIVERY",
                "Meeting delivery evidence",
                "Meeting",
                "Evidence of a confirmed or completed consultation.",
                "subject",
                "meetingType",
                "startsAt",
                "attendees",
                "outcome"
            ),
            Template(
                "SUBSCRIPTION-KPI",
                "Subscription KPI evidence",
                "Subscription",
                "Evidence for an active annual personal subscription.",
                "userId",
                "organizationId",
                "startsAt",
                "expiresAt",
                "paymentReference"
            ),
            Template(
                "CONTACT-INTAKE",
                "Contact request evidence",
                "ContactRequest",
                "Evidence of public DMA-style intake and handling.",
                "organizationType",
                "sector",
                "region",
                "mainNeed",
                "handledAt"
            ),
            Template(
                "KPI-PERIOD",
                "KPI reporting period dossier",
                "KpiPeriod",
                "Periodic grant/KPI evidence dossier.",
                "reportingPeriod",
                "kpiCategory",
                "metricValue",
                "source",
                "approvedBy"
            ),
            Template(
                "KPI-CONTACT-BREAKDOWN",
                "Contact intake breakdown",
                "KpiPeriod",
                "Contacts grouped by sector, region, organisation type and DMA need.",
                "reportingPeriod",
                "totalContacts",
                "bySector",
                "byRegion",
                "byOrganizationType",
                "byDmaCategory",
                "sourceQuery"
            ),
            Template(
                "KPI-TICKET-BREAKDOWN",
                "Help-desk ticket breakdown",
                "KpiPeriod",
                "Tickets grouped by category, status, priority, assignee and organisation type.",
                "reportingPeriod",
                "totalTickets",
                "byCategory",
                "byStatus",
                "byPriority",
                "byAssignee",
                "byOrganizationType"
            ),
            Template(
                "KPI-MEETING-REFERRAL",
                "Meetings and referrals breakdown",
                "KpiPeriod",
                "Completed meetings, consultation formats and referrals grouped for programme reporting.",
                "reportingPeriod",
                "requested",
                "completed",
                "byMeetingType",
                "referralsByDestination",
                "sourceQuery"
            ),
            Template(
                "KPI-SUBSCRIPTION-COHORT",
                "Subscription cohort report",
                "KpiPeriod",
                "Invited, activated, expired and cancelled subscriptions for a reporting period.",
                "reportingPeriod",
                "invited",
                "activated",
                "expired",
                "cancelled",
                "activeAtPeriodEnd"
            ),
        };
        var existingTemplateCodes = await db.EvidenceTemplates.Select(x => x.Code).ToListAsync();
        db.EvidenceTemplates.AddRange(
            evidenceTemplates.Where(x =>
                !existingTemplateCodes.Contains(x.Code, StringComparer.OrdinalIgnoreCase)
            )
        );
        if (!await db.ServiceCatalogueItems.AnyAsync())
        {
            var seeds = new[]
            {
                (
                    "ai-readiness",
                    "AI Readiness",
                    "Проценка на AI зрелост",
                    "Vlerësimi i gatishmërisë për AI"
                ),
                (
                    "ai-act-compliance",
                    "AI Act & Compliance",
                    "AI Act и усогласеност",
                    "AI Act dhe pajtueshmëria"
                ),
                (
                    "test-before-invest",
                    "Test Before Invest",
                    "Тестирај пред инвестиција",
                    "Testo para investimit"
                ),
                ("digital-roadmap", "Digital Roadmap", "Дигитален патоказ", "Udhërrëfyes digjital"),
            };
            foreach (var seed in seeds)
            {
                var item = new ServiceCatalogueItem
                {
                    Slug = seed.Item1,
                    Category = "DigitalInnovation",
                };
                db.Add(item);
                db.Translations.AddRange(
                    new Translation
                    {
                        EntityType = nameof(ServiceCatalogueItem),
                        EntityId = item.Id,
                        Language = "en",
                        FieldName = "title",
                        Value = seed.Item2,
                    },
                    new Translation
                    {
                        EntityType = nameof(ServiceCatalogueItem),
                        EntityId = item.Id,
                        Language = "mk",
                        FieldName = "title",
                        Value = seed.Item3,
                    },
                    new Translation
                    {
                        EntityType = nameof(ServiceCatalogueItem),
                        EntityId = item.Id,
                        Language = "sq",
                        FieldName = "title",
                        Value = seed.Item4,
                    }
                );
            }
        }
        await db.SaveChangesAsync();
    }

    private static async Task EnsureLocalSchemaAsync(PortalDbContext db)
    {
        if (
            db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) != true
        )
            return;

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "AccountChangeRequests" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_AccountChangeRequests" PRIMARY KEY,
                "UserId" TEXT NOT NULL,
                "OrganizationId" TEXT NOT NULL,
                "RequestType" TEXT NOT NULL,
                "Details" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "DecisionNote" TEXT NULL,
                "DecidedBy" TEXT NULL,
                "DecidedAt" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_AccountChangeRequests_UserId_Status"
                ON "AccountChangeRequests" ("UserId", "Status");
            """
        );

        // Notifications existed before IsRead/ActionUrl were added to the Notification model —
        // EnsureCreatedAsync only creates tables that don't exist yet, so an already-existing
        // local dev database needs these two columns added by hand, once. We check first so a
        // healthy, already-migrated database never even attempts (and logs) a failing ALTER TABLE.
        await AddColumnIfMissingAsync(db, "Notifications", "IsRead", "INTEGER NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync(db, "Notifications", "ActionUrl", "TEXT NULL");
        await AddColumnIfMissingAsync(db, "Meetings", "CreatedByUserId", "TEXT NULL");
    }

    private static async Task<bool> ColumnExistsAsync(PortalDbContext db, string table, string column)
    {
        await using var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed)
            await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""PRAGMA table_info("{table}");""";
            await using var reader = await command.ExecuteReaderAsync();
            var nameOrdinal = -1;
            while (await reader.ReadAsync())
            {
                if (nameOrdinal < 0)
                    nameOrdinal = reader.GetOrdinal("name");
                if (string.Equals(reader.GetString(nameOrdinal), column, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        finally
        {
            if (wasClosed)
                await connection.CloseAsync();
        }
    }

    private static async Task AddColumnIfMissingAsync(
        PortalDbContext db,
        string table,
        string column,
        string columnDefinition
    )
    {
        if (await ColumnExistsAsync(db, table, column))
            return;
        await db.Database.ExecuteSqlRawAsync(
            $"""ALTER TABLE "{table}" ADD COLUMN "{column}" {columnDefinition};"""
        );
    }

    private static async Task EnsureDemoClientAsync(
        UserManager<AppUser> users,
        PortalDbContext db,
        AppUser admin,
        IConfiguration configuration
    )
    {
        var now = DateTimeOffset.UtcNow;
        var email = configuration["DemoAccount:Email"] ?? "client@digitmak.mk";
        var password = configuration["DemoAccount:Password"] ?? "DigitMak!2026Client";
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                EmailVerifiedAt = now,
                Status = UserStatuses.Active,
                FirstName = "Demo",
                LastName = "Client",
                PreferredLanguage = "mk",
                TermsAcceptedAt = now,
                LockoutEnabled = false,
            };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "The local demo client could not be created: "
                        + string.Join(", ", result.Errors.Select(x => x.Description))
                );
        }

        var demoRoles = await users.GetRolesAsync(user);
        foreach (var role in demoRoles.Where(role => role != "Client"))
        {
            var removeRole = await users.RemoveFromRoleAsync(user, role);
            if (!removeRole.Succeeded)
                throw new InvalidOperationException(
                    $"The local demo client role {role} could not be removed: "
                        + string.Join(", ", removeRole.Errors.Select(x => x.Description))
                );
        }
        if (!await users.IsInRoleAsync(user, "Client"))
        {
            var addClientRole = await users.AddToRoleAsync(user, "Client");
            if (!addClientRole.Succeeded)
                throw new InvalidOperationException(
                    "The local demo client role could not be restored: "
                        + string.Join(", ", addClientRole.Errors.Select(x => x.Description))
                );
        }

        user.EmailConfirmed = true;
        user.EmailVerifiedAt ??= now;
        user.Status = UserStatuses.Active;
        user.LockoutEnabled = false;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        var updateResult = await users.UpdateAsync(user);
        if (!updateResult.Succeeded)
            throw new InvalidOperationException(
                "The local demo client could not be refreshed: "
                    + string.Join(", ", updateResult.Errors.Select(x => x.Description))
            );

        var organization = await db.Organizations.SingleOrDefaultAsync(x =>
            x.CreatedByUserId == user.Id && x.Name == "DigitMak Demo Organization"
        );
        if (organization is null)
        {
            organization = new Organization
            {
                Name = "DigitMak Demo Organization",
                Type = "SME",
                Sector = "Information technology",
                Municipality = "Skopje",
                Region = "Skopje",
                EmployeeCount = 25,
                Status = "Approved",
                CreatedByUserId = user.Id,
                ApprovedBy = admin.Id,
                ApprovedAt = now,
            };
            db.Organizations.Add(organization);
        }
        else
        {
            organization.Status = "Approved";
            organization.ApprovedBy ??= admin.Id;
            organization.ApprovedAt ??= now;
        }

        user.OrganizationId = organization.Id;
        if (
            !await db.OrganizationMembers.AnyAsync(x =>
                x.OrganizationId == organization.Id && x.UserId == user.Id
            )
        )
            db.OrganizationMembers.Add(
                new OrganizationMember
                {
                    OrganizationId = organization.Id,
                    UserId = user.Id,
                    MemberStatus = "Active",
                    IsPrimaryContact = true,
                }
            );
        if (
            !await db.Subscriptions.AnyAsync(x =>
                x.UserId == user.Id && x.Status == "Active" && x.ExpiresAt > now
            )
        )
            db.Subscriptions.Add(
                new Subscription
                {
                    UserId = user.Id,
                    OrganizationId = organization.Id,
                    Status = "Active",
                    StartsAt = now,
                    ExpiresAt = now.AddMonths(12),
                    OfflinePaymentReference = "LOCAL-DEMO",
                    PaymentNote = "Development-only seeded subscription",
                    InvitedBy = admin.Id,
                    ActivatedBy = admin.Id,
                    ActivatedAt = now,
                }
            );
    }

    private static EvidenceTemplate Template(
        string code,
        string name,
        string entityType,
        string description,
        params string[] metadata
    ) =>
        new()
        {
            Code = code,
            Name = name,
            RelatedEntityType = entityType,
            Description = description,
            RequiredMetadataJson = JsonSerializer.Serialize(metadata),
            IsActive = true,
        };
}
