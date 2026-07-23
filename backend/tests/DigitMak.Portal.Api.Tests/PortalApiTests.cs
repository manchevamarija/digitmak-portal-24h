using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DigitMak.Portal.Api.Domain.Entities;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DigitMak.Portal.Api.Tests;

public class PortalApiTests : IClassFixture<PortalFactory>
{
    private readonly HttpClient client;
    private readonly PortalFactory factory;

    public PortalApiTests(PortalFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_is_available() =>
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);

    [Fact]
    public async Task Liveness_and_readiness_expose_detailed_checks()
    {
        var live = await client.GetFromJsonAsync<JsonElement>("/health/live");
        Assert.Equal("Healthy", live.GetProperty("status").GetString());
        Assert.True(live.GetProperty("checks").TryGetProperty("application", out _));
        var ready = await client.GetFromJsonAsync<JsonElement>("/health/ready");
        Assert.NotEqual("Unhealthy", ready.GetProperty("status").GetString());
        Assert.True(ready.GetProperty("checks").TryGetProperty("database", out _));
        Assert.True(ready.GetProperty("checks").TryGetProperty("storage", out _));
    }

    [Fact]
    public async Task Locales_include_required_languages() =>
        Assert.Equal(
            ["mk", "en", "sq"],
            (await client.GetFromJsonAsync<string[]>("/api/public/settings/locales"))!
        );

    [Fact]
    public async Task Contact_requires_consent()
    {
        var r = await client.PostAsJsonAsync(
            "/api/public/contact-requests",
            new
            {
                organizationName = "Test",
                organizationType = "SME",
                contactName = "Tester",
                email = "test@example.com",
                mainNeed = "AI",
                challengeDescription = "Test",
                consentToContact = false,
                privacyPolicyAccepted = true,
            }
        );
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Tickets_require_authentication() =>
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/tickets/my")).StatusCode
        );

    [Fact]
    public async Task Organization_requires_authentication() =>
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/organizations/my")).StatusCode
        );

    [Fact]
    public async Task Subscription_requires_authentication() =>
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/subscriptions/my")).StatusCode
        );

    [Fact]
    public async Task Files_require_authentication() =>
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync($"/api/files/{Guid.NewGuid()}")).StatusCode
        );

    [Fact]
    public async Task Security_headers_are_present()
    {
        var r = await client.GetAsync("/health");
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task Valid_contact_is_created()
    {
        var r = await client.PostAsJsonAsync(
            "/api/public/contact-requests",
            new
            {
                organizationName = "Test",
                organizationType = "SME",
                contactName = "Tester",
                email = "test@example.com",
                mainNeed = "AI",
                challengeDescription = "We need practical guidance",
                consentToContact = true,
                privacyPolicyAccepted = true,
            }
        );
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    [Fact]
    public async Task Contact_preserves_one_of_the_six_explicit_dma_categories()
    {
        var response = await client.PostAsJsonAsync(
            "/api/public/contact-requests",
            new
            {
                organizationName = "Green Test",
                organizationType = "SME",
                contactName = "DMA Tester",
                email = $"dma-{Guid.NewGuid():N}@example.com",
                preferredLanguage = "mk",
                digitalMaturityRating = 3,
                dmaCategory = "GREEN_DIGITALISATION",
                mainNeed = "Digitalization",
                challengeDescription = "We need greener digital processes",
                consentToContact = true,
                privacyPolicyAccepted = true,
            }
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var contact = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("GREEN_DIGITALISATION", contact.GetProperty("dmaCategory").GetString());
    }

    [Fact]
    public async Task Admin_session_issues_secure_refresh_cookie()
    {
        var r = await client.PostAsJsonAsync(
            "/api/auth/session",
            new { email = "admin@digitmak.mk", password = "DigitMak!2026Admin" }
        );
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains(
            r.Headers.GetValues("Set-Cookie"),
            value =>
                value.Contains("digitmak.refresh=")
                && value.Contains("httponly", StringComparison.OrdinalIgnoreCase)
                && value.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public async Task Pdf_login_route_alias_issues_a_session()
    {
        var r = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "admin@digitmak.mk", password = "DigitMak!2026Admin" }
        );
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("accessToken").GetString()));
    }

    [Fact]
    public async Task Admin_configures_payment_instructions_visible_to_authenticated_clients()
    {
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/subscriptions/payment-instructions")).StatusCode
        );
        var admin = factory.CreateClient();
        var adminSession = await admin.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "admin@digitmak.mk", password = "DigitMak!2026Admin" }
        );
        var adminJson = await adminSession.Content.ReadFromJsonAsync<JsonElement>();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            adminJson.GetProperty("accessToken").GetString()
        );
        foreach (
            var setting in new[]
            {
                (Key: "PAYMENT_RECIPIENT", Value: "DigitMak Test Recipient"),
                (Key: "PAYMENT_ACCOUNT", Value: "TEST-ACCOUNT-001"),
                (Key: "PAYMENT_AMOUNT", Value: "12000"),
                (Key: "PAYMENT_CURRENCY", Value: "MKD"),
            }
        )
            Assert.Equal(
                HttpStatusCode.OK,
                (
                    await admin.PutAsJsonAsync(
                        $"/api/admin/settings/{setting.Key}",
                        new { value = setting.Value, description = "Test payment instruction" }
                    )
                ).StatusCode
            );

        var subscriber = factory.CreateClient();
        var subscriberSession = await subscriber.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "client@digitmak.mk", password = "DigitMak!2026Client" }
        );
        var subscriberJson = await subscriberSession.Content.ReadFromJsonAsync<JsonElement>();
        subscriber.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            subscriberJson.GetProperty("accessToken").GetString()
        );
        var instructions = await subscriber.GetFromJsonAsync<JsonElement>(
            "/api/subscriptions/payment-instructions"
        );
        Assert.True(instructions.GetProperty("isConfigured").GetBoolean());
        Assert.Equal("DigitMak Test Recipient", instructions.GetProperty("recipient").GetString());
        Assert.Equal("TEST-ACCOUNT-001", instructions.GetProperty("account").GetString());
        Assert.Equal("12000", instructions.GetProperty("amount").GetString());
        Assert.Equal("MKD", instructions.GetProperty("currency").GetString());
    }

    [Fact]
    public async Task Local_demo_client_can_login_and_create_a_ticket()
    {
        var authenticated = factory.CreateClient();
        for (var attempt = 0; attempt < 7; attempt++)
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (
                    await authenticated.PostAsJsonAsync(
                        "/api/auth/login",
                        new { email = "client@digitmak.mk", password = "WrongPassword!2026" }
                    )
                ).StatusCode
            );
        var session = await authenticated.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "client@digitmak.mk", password = "DigitMak!2026Client" }
        );
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        var sessionJson = await session.Content.ReadFromJsonAsync<JsonElement>();
        authenticated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            sessionJson.GetProperty("accessToken").GetString()
        );
        var profile = await authenticated.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(
            ["Client"],
            profile
                .GetProperty("roles")
                .EnumerateArray()
                .Select(role => role.GetString()!)
                .ToArray()
        );
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await authenticated.GetAsync("/api/staff/tickets")).StatusCode
        );
        var response = await authenticated.PostAsJsonAsync(
            "/api/tickets",
            new
            {
                category = "AI_READINESS",
                title = "Local demo ticket",
                description = "End-to-end development verification",
                priority = "Normal",
            }
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_reactivate_a_suspended_organization()
    {
        var admin = await AdminClient();
        var organizations = await admin.GetFromJsonAsync<JsonElement[]>("/api/admin/organizations");
        var organization = organizations!.Single(item =>
            item.GetProperty("name").GetString() == "DigitMak Demo Organization"
        );
        var id = organization.GetProperty("id").GetGuid();

        Assert.Equal(
            HttpStatusCode.OK,
            (await admin.PostAsync($"/api/admin/organizations/{id}/suspend", null)).StatusCode
        );
        var response = await admin.PostAsync($"/api/admin/organizations/{id}/reactivate", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Registration_records_versioned_legal_consent()
    {
        var email = $"consent-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email,
                password = "ConsentTest!2026",
                firstName = "Consent",
                lastName = "Tester",
                preferredLanguage = "en",
                termsAccepted = true,
                termsVersion = LegalDocumentVersions.Terms,
                privacyVersion = LegalDocumentVersions.Privacy,
            }
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == email);
        var audit = await db.AuditLogs.SingleAsync(x =>
            x.ActorUserId == user.Id && x.Action == "LegalConsentAccepted"
        );
        Assert.Contains(LegalDocumentVersions.Terms, audit.MetadataJson);
        Assert.Contains(LegalDocumentVersions.Privacy, audit.MetadataJson);
    }

    [Fact]
    public async Task Integration_status_is_explicit_for_optional_providers()
    {
        var authenticated = await AdminClient();
        var status = await authenticated.GetFromJsonAsync<JsonElement>("/api/integrations/status");
        Assert.True(
            status
                .GetProperty("calendars")
                .GetProperty("ics")
                .GetProperty("configured")
                .GetBoolean()
        );
        Assert.False(
            status
                .GetProperty("calendars")
                .GetProperty("google")
                .GetProperty("configured")
                .GetBoolean()
        );
        Assert.False(
            status
                .GetProperty("calendars")
                .GetProperty("microsoft")
                .GetProperty("configured")
                .GetBoolean()
        );
    }

    [Fact]
    public async Task Invalid_maturity_rating_is_rejected()
    {
        var r = await client.PostAsJsonAsync(
            "/api/public/contact-requests",
            new
            {
                organizationName = "Test",
                organizationType = "SME",
                contactName = "Tester",
                email = "test@example.com",
                preferredLanguage = "mk",
                digitalMaturityRating = 7,
                mainNeed = "AI",
                challengeDescription = "Challenge",
                consentToContact = true,
                privacyPolicyAccepted = true,
            }
        );
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Existing_organization_member_gets_an_explicit_join_conflict()
    {
        var authenticated = factory.CreateClient();
        var session = await authenticated.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "client@digitmak.mk", password = "DigitMak!2026Client" }
        );
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        var sessionJson = await session.Content.ReadFromJsonAsync<JsonElement>();
        authenticated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            sessionJson.GetProperty("accessToken").GetString()
        );
        var organization = await authenticated.GetFromJsonAsync<JsonElement>(
            "/api/organizations/my"
        );
        var response = await authenticated.PostAsync(
            $"/api/organizations/{organization.GetProperty("id").GetGuid()}/join",
            null
        );
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ORGANIZATION_ALREADY_ASSIGNED", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Admin_endpoints_require_admin() =>
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/admin/reports/kpis")).StatusCode
        );

    [Fact]
    public async Task Invalid_verification_token_is_bad_request()
    {
        var r = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new { userId = Guid.NewGuid().ToString(), token = "not-base64" }
        );
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Admin_can_read_extended_kpis()
    {
        var authenticated = await AdminClient();
        var r = await authenticated.GetAsync("/api/admin/reports/kpis");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("publicInstitutions", out _));
        Assert.True(json.TryGetProperty("aiActRequests", out _));
    }

    [Fact]
    public async Task Admin_can_read_all_detailed_report_breakdowns()
    {
        var authenticated = await AdminClient();
        var contacts = await authenticated.GetFromJsonAsync<JsonElement>(
            "/api/admin/reports/contacts"
        );
        Assert.True(contacts.TryGetProperty("bySector", out _));
        Assert.True(contacts.TryGetProperty("byRegion", out _));
        Assert.True(contacts.TryGetProperty("byNeed", out _));
        Assert.True(contacts.TryGetProperty("byDmaCategory", out _));
        var tickets = await authenticated.GetFromJsonAsync<JsonElement>(
            "/api/admin/reports/tickets-detailed"
        );
        Assert.True(tickets.TryGetProperty("byAssignee", out _));
        Assert.True(tickets.TryGetProperty("byOrganizationType", out _));
        var meetings = await authenticated.GetFromJsonAsync<JsonElement>(
            "/api/admin/reports/meetings"
        );
        Assert.True(meetings.TryGetProperty("byStatus", out _));
        Assert.True(meetings.TryGetProperty("byType", out _));
        Assert.Equal(
            HttpStatusCode.OK,
            (await authenticated.GetAsync("/api/admin/reports/referrals")).StatusCode
        );
    }

    [Fact]
    public async Task Admin_can_export_pdf_compatible_csv_and_excel_reports()
    {
        var authenticated = await AdminClient();

        var legacyCsv = await authenticated.GetAsync("/api/admin/reports/export");
        Assert.Equal(HttpStatusCode.OK, legacyCsv.StatusCode);
        Assert.Equal("text/csv", legacyCsv.Content.Headers.ContentType?.MediaType);
        var legacyBytes = await legacyCsv.Content.ReadAsByteArrayAsync();
        Assert.True(legacyBytes.Length > 3);
        Assert.Equal(Encoding.UTF8.GetPreamble(), legacyBytes[..3]);
        Assert.Contains("Број на тикет", Encoding.UTF8.GetString(legacyBytes));

        var csv = await authenticated.GetAsync("/api/admin/reports/export/contacts?format=csv");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);

        var excel = await authenticated.GetAsync("/api/admin/reports/export/contacts");
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            excel.Content.Headers.ContentType?.MediaType
        );
        var excelBytes = await excel.Content.ReadAsByteArrayAsync();
        Assert.Equal((byte)'P', excelBytes[0]);
        Assert.Equal((byte)'K', excelBytes[1]);
    }

    [Fact]
    public async Task Admin_can_download_seeded_evidence_template()
    {
        var authenticated = await AdminClient();
        var templates = await authenticated.GetFromJsonAsync<JsonElement[]>(
            "/api/admin/evidence-templates"
        );
        Assert.NotEmpty(templates!);
        var response = await authenticated.GetAsync(
            $"/api/admin/evidence-templates/{templates![0].GetProperty("id").GetGuid()}/blank"
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType
        );
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1_000);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);

        var csv = await authenticated.GetAsync(
            $"/api/admin/evidence-templates/{templates[0].GetProperty("id").GetGuid()}/blank?format=csv"
        );
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Field,Value", await csv.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_can_export_staff_calendar()
    {
        var authenticated = await AdminClient();
        var r = await authenticated.GetAsync("/api/staff/meetings/calendar.ics");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("text/calendar", r.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Admin_can_upload_and_list_evidence()
    {
        var authenticated = await AdminClient();
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "evidence.png");
        var relatedId = Guid.NewGuid();
        var created = await authenticated.PostAsync(
            $"/api/admin/evidence?relatedEntityType=KpiPeriod&relatedEntityId={relatedId}&kpiCategory=AI_HELP_DESK&reportingPeriod=2026-Q3&templateType=Manual",
            content
        );
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var listed = await authenticated.GetFromJsonAsync<JsonElement[]>("/api/admin/evidence");
        Assert.Contains(
            listed!,
            item => item.GetProperty("relatedEntityId").GetGuid() == relatedId
        );
    }

    [Fact]
    public async Task Active_subscriber_can_create_ticket()
    {
        var email = $"subscriber-{Guid.NewGuid():N}@example.com";
        const string password = "Subscriber!2026";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Active",
                LastName = "Subscriber",
                TermsAcceptedAt = DateTimeOffset.UtcNow,
            };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            await users.AddToRoleAsync(user, "Client");
            var organization = new Organization
            {
                Name = "Approved Test Org",
                Type = "SME",
                Status = "Approved",
                CreatedByUserId = user.Id,
            };
            db.Organizations.Add(organization);
            user.OrganizationId = organization.Id;
            db.OrganizationMembers.Add(
                new OrganizationMember
                {
                    OrganizationId = organization.Id,
                    UserId = user.Id,
                    IsPrimaryContact = true,
                }
            );
            db.Subscriptions.Add(
                new Subscription
                {
                    UserId = user.Id,
                    OrganizationId = organization.Id,
                    Status = "Active",
                    StartsAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMonths(12),
                }
            );
            await db.SaveChangesAsync();
        }
        var authenticated = factory.CreateClient();
        var session = await authenticated.PostAsJsonAsync(
            "/api/auth/session",
            new { email, password }
        );
        var json = await session.Content.ReadFromJsonAsync<JsonElement>();
        authenticated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            json.GetProperty("accessToken").GetString()
        );
        var response = await authenticated.PostAsJsonAsync(
            "/api/tickets",
            new
            {
                category = "AI_READINESS",
                title = "Readiness assessment",
                description = "We need an AI readiness assessment",
                priority = "High",
            }
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_ticket_on_behalf_of_client_with_audit_record()
    {
        Guid clientUserId;
        Guid organizationId;
        Guid adminUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var clientUser = await db.Users.SingleAsync(x => x.Email == "client@digitmak.mk");
            var adminUser = await db.Users.SingleAsync(x => x.Email == "admin@digitmak.mk");
            clientUserId = clientUser.Id;
            organizationId = clientUser.OrganizationId!.Value;
            adminUserId = adminUser.Id;
        }

        var admin = await AdminClient();
        var response = await admin.PostAsJsonAsync(
            "/api/admin/tickets",
            new
            {
                userId = clientUserId,
                organizationId,
                category = "AI_READINESS",
                title = "Administrator-created client ticket",
                description = "Created on behalf of the demo client",
                priority = "Normal",
            }
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ticketId = created.GetProperty("id").GetGuid();
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PortalDbContext>();
        var ticket = await verifyDb.Tickets.SingleAsync(x => x.Id == ticketId);
        var audit = await verifyDb.AuditLogs.SingleAsync(x =>
            x.EntityId == ticketId.ToString() && x.Action == "TicketCreatedOnBehalf"
        );
        Assert.Equal(clientUserId, ticket.CreatedByUserId);
        Assert.Equal(organizationId, ticket.OrganizationId);
        Assert.Equal(adminUserId, audit.ActorUserId);
        Assert.Contains(clientUserId.ToString(), audit.MetadataJson);
    }

    [Fact]
    public async Task Ticket_attachment_is_linked_listed_and_downloadable()
    {
        var email = $"attachment-{Guid.NewGuid():N}@example.com";
        const string password = "Subscriber!2026";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Attachment",
                LastName = "Tester",
                TermsAcceptedAt = DateTimeOffset.UtcNow,
            };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            await users.AddToRoleAsync(user, "Client");
            var organization = new Organization
            {
                Name = "Attachment Org",
                Type = "SME",
                Status = "Approved",
                CreatedByUserId = user.Id,
            };
            db.Organizations.Add(organization);
            user.OrganizationId = organization.Id;
            db.OrganizationMembers.Add(
                new OrganizationMember
                {
                    OrganizationId = organization.Id,
                    UserId = user.Id,
                    IsPrimaryContact = true,
                }
            );
            db.Subscriptions.Add(
                new Subscription
                {
                    UserId = user.Id,
                    OrganizationId = organization.Id,
                    Status = "Active",
                    StartsAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMonths(12),
                }
            );
            await db.SaveChangesAsync();
        }
        var authenticated = factory.CreateClient();
        var session = await authenticated.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password }
        );
        var sessionJson = await session.Content.ReadFromJsonAsync<JsonElement>();
        authenticated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            sessionJson.GetProperty("accessToken").GetString()
        );
        var created = await authenticated.PostAsJsonAsync(
            "/api/tickets",
            new
            {
                category = "AI_READINESS",
                title = "Attachment ticket",
                description = "A ticket with a linked attachment",
                priority = "Normal",
            }
        );
        var ticket = await created.Content.ReadFromJsonAsync<JsonElement>();
        var ticketId = ticket.GetProperty("id").GetGuid();
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "ticket-evidence.png");
        Assert.Equal(
            HttpStatusCode.Created,
            (
                await authenticated.PostAsync($"/api/tickets/{ticketId}/attachments", content)
            ).StatusCode
        );
        var attachments = await authenticated.GetFromJsonAsync<JsonElement[]>(
            $"/api/tickets/{ticketId}/attachments"
        );
        Assert.Single(attachments!);
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await authenticated.GetAsync(
                    $"/api/files/{attachments![0].GetProperty("fileId").GetGuid()}"
                )
            ).StatusCode
        );
        var admin = factory.CreateClient();
        var adminSession = await admin.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "admin@digitmak.mk", password = "DigitMak!2026Admin" }
        );
        var adminSessionJson = await adminSession.Content.ReadFromJsonAsync<JsonElement>();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            adminSessionJson.GetProperty("accessToken").GetString()
        );
        var allAttachments = await admin.GetFromJsonAsync<JsonElement[]>(
            "/api/admin/ticket-attachments"
        );
        Assert.Contains(
            allAttachments!,
            item => item.GetProperty("originalFilename").GetString() == "ticket-evidence.png"
        );
    }

    [Fact]
    public async Task Complete_client_to_admin_reply_flow_is_visible_to_the_client()
    {
        var email = $"e2e-{Guid.NewGuid():N}@example.com";
        const string password = "EndToEnd!2026";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "End",
                LastName = "ToEnd",
                TermsAcceptedAt = DateTimeOffset.UtcNow,
            };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            await users.AddToRoleAsync(user, "Client");
            var organization = new Organization
            {
                Name = "E2E Organisation",
                Type = "SME",
                Status = "Approved",
                CreatedByUserId = user.Id,
            };
            db.Organizations.Add(organization);
            user.OrganizationId = organization.Id;
            db.OrganizationMembers.Add(
                new OrganizationMember
                {
                    OrganizationId = organization.Id,
                    UserId = user.Id,
                    IsPrimaryContact = true,
                }
            );
            db.Subscriptions.Add(
                new Subscription
                {
                    UserId = user.Id,
                    OrganizationId = organization.Id,
                    Status = "Active",
                    StartsAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMonths(12),
                }
            );
            await db.SaveChangesAsync();
        }

        var clientSession = factory.CreateClient();
        var login = await clientSession.PostAsJsonAsync("/api/auth/login", new { email, password });
        var loginJson = await login.Content.ReadFromJsonAsync<JsonElement>();
        clientSession.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            loginJson.GetProperty("accessToken").GetString()
        );
        var created = await clientSession.PostAsJsonAsync(
            "/api/tickets",
            new
            {
                category = "AI_USE_CASE",
                title = "End-to-end ticket",
                description = "Please review this AI use case",
                priority = "Normal",
            }
        );
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var ticket = await created.Content.ReadFromJsonAsync<JsonElement>();
        var ticketId = ticket.GetProperty("id").GetGuid();

        var admin = await AdminClient();
        Assert.Equal(
            HttpStatusCode.Created,
            (
                await admin.PostAsJsonAsync(
                    $"/api/staff/tickets/{ticketId}/messages",
                    new { body = "The DigitMak team reviewed the request." }
                )
            ).StatusCode
        );

        var messages = await clientSession.GetFromJsonAsync<JsonElement[]>(
            $"/api/tickets/{ticketId}/messages"
        );
        Assert.Contains(
            messages!,
            message =>
                message.GetProperty("messageType").GetString() == "StaffReply"
                && message.GetProperty("body").GetString()
                    == "The DigitMak team reviewed the request."
        );
    }

    [Fact]
    public async Task Pdf_subscription_invitation_route_is_supported()
    {
        var email = $"invite-{Guid.NewGuid():N}@example.com";
        const string password = "Subscriber!2026";
        var token = Convert
            .ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace("/", "_")
            .Replace("+", "-");
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Invite",
                LastName = "Tester",
                TermsAcceptedAt = DateTimeOffset.UtcNow,
            };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            await users.AddToRoleAsync(user, "Client");
            var organization = new Organization
            {
                Name = "Invitation Org",
                Type = "SME",
                Status = "Approved",
                CreatedByUserId = user.Id,
            };
            db.Organizations.Add(organization);
            user.OrganizationId = organization.Id;
            db.OrganizationMembers.Add(
                new OrganizationMember
                {
                    OrganizationId = organization.Id,
                    UserId = user.Id,
                    IsPrimaryContact = true,
                }
            );
            db.SubscriptionInvitations.Add(
                new SubscriptionInvitation
                {
                    UserId = user.Id,
                    OrganizationId = organization.Id,
                    TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
                    CreatedBy = user.Id,
                }
            );
            await db.SaveChangesAsync();
        }
        var authenticated = factory.CreateClient();
        var session = await authenticated.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password }
        );
        var sessionJson = await session.Content.ReadFromJsonAsync<JsonElement>();
        authenticated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            sessionJson.GetProperty("accessToken").GetString()
        );
        Assert.Equal(
            HttpStatusCode.Created,
            (
                await authenticated.PostAsync(
                    $"/api/subscription-invitations/{Uri.EscapeDataString(token)}/accept",
                    null
                )
            ).StatusCode
        );
    }

    [Fact]
    public async Task Expert_only_sees_assigned_tickets_and_status_creates_system_event()
    {
        var email = $"expert-{Guid.NewGuid():N}@example.com";
        const string password = "ExpertPass!2026";
        Guid visibleId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var expert = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Expert",
                LastName = "Tester",
                TermsAcceptedAt = DateTimeOffset.UtcNow,
            };
            Assert.True((await users.CreateAsync(expert, password)).Succeeded);
            await users.AddToRoleAsync(expert, "Expert");
            var organization = new Organization
            {
                Name = "Expert Access Org",
                Type = "SME",
                Status = "Approved",
                CreatedByUserId = expert.Id,
            };
            db.Organizations.Add(organization);
            var visible = new Ticket
            {
                TicketNumber = $"DM-{Guid.NewGuid():N}",
                OrganizationId = organization.Id,
                CreatedByUserId = expert.Id,
                Title = "Assigned",
                Description = "Visible",
                AssignedExpertId = expert.Id,
                Status = "Assigned",
            };
            var hidden = new Ticket
            {
                TicketNumber = $"DM-{Guid.NewGuid():N}",
                OrganizationId = organization.Id,
                CreatedByUserId = expert.Id,
                Title = "Unassigned",
                Description = "Hidden",
            };
            db.Tickets.AddRange(visible, hidden);
            await db.SaveChangesAsync();
            visibleId = visible.Id;
        }
        var authenticated = factory.CreateClient();
        var session = await authenticated.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password }
        );
        var sessionJson = await session.Content.ReadFromJsonAsync<JsonElement>();
        authenticated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            sessionJson.GetProperty("accessToken").GetString()
        );
        var tickets = await authenticated.GetFromJsonAsync<JsonElement[]>("/api/staff/tickets");
        Assert.Single(tickets!);
        Assert.Equal(visibleId, tickets![0].GetProperty("id").GetGuid());
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (
                await authenticated.PostAsJsonAsync(
                    $"/api/staff/tickets/{visibleId}/assign",
                    new { agentId = (Guid?)null, expertId = (Guid?)null }
                )
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await authenticated.PatchAsync(
                    $"/api/staff/tickets/{visibleId}/status?status=InProgress",
                    null
                )
            ).StatusCode
        );
        using var check = factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<PortalDbContext>();
        Assert.True(
            await checkDb.TicketMessages.AnyAsync(x =>
                x.TicketId == visibleId && x.MessageType == "SystemEvent"
            )
        );
    }

    [Fact]
    public async Task Deactivation_immediately_rejects_an_already_issued_access_token_and_revokes_refresh_tokens()
    {
        var email = $"deactivate-{Guid.NewGuid():N}@example.com";
        const string password = "Deactivate!2026";
        Guid userId;
        DateTimeOffset createdAt;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Deactivate",
                LastName = "Tester",
                TermsAcceptedAt = DateTimeOffset.UtcNow,
            };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            await users.AddToRoleAsync(user, "Client");
            userId = user.Id;
            createdAt = user.CreatedAt;
            Assert.NotEqual(default, user.CreatedAt);
            Assert.NotEqual(default, user.UpdatedAt);
        }

        var authenticated = factory.CreateClient();
        var session = await authenticated.PostAsJsonAsync(
            "/api/auth/session",
            new { email, password }
        );
        var sessionJson = await session.Content.ReadFromJsonAsync<JsonElement>();
        authenticated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            sessionJson.GetProperty("accessToken").GetString()
        );
        Assert.Equal(HttpStatusCode.OK, (await authenticated.GetAsync("/api/auth/me")).StatusCode);

        var admin = await AdminClient();
        var deactivated = await admin.PatchAsJsonAsync(
            $"/api/admin/users/{userId}",
            new
            {
                status = "Inactive",
                preferredLanguage = "mk",
                phone = (string?)null,
            }
        );
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await authenticated.GetAsync("/api/auth/me")).StatusCode
        );

        using var check = factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<PortalDbContext>();
        var stored = await db.Users.SingleAsync(user => user.Id == userId);
        Assert.Equal(UserStatuses.Inactive, stored.Status);
        Assert.NotNull(stored.EmailVerifiedAt);
        Assert.True(stored.UpdatedAt >= createdAt);
        Assert.All(
            await db.RefreshTokens.Where(token => token.UserId == userId).ToListAsync(),
            token => Assert.NotNull(token.RevokedAt)
        );
    }

    [Fact]
    public async Task Email_verification_records_exact_timestamp_and_activates_status()
    {
        var email = $"verify-{Guid.NewGuid():N}@example.com";
        const string password = "VerifyEmail!2026";
        var registered = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email,
                password,
                firstName = "Verify",
                lastName = "Tester",
                preferredLanguage = "mk",
                termsAccepted = true,
            }
        );
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        string userId;
        string verificationToken;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await users.FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.Equal(UserStatuses.PendingVerification, user!.Status);
            Assert.Null(user.EmailVerifiedAt);
            userId = user.Id.ToString();
            verificationToken = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(await users.GenerateEmailConfirmationTokenAsync(user))
            );
        }

        Assert.Equal(
            HttpStatusCode.NoContent,
            (
                await client.PostAsJsonAsync(
                    "/api/auth/verify-email",
                    new { userId, token = verificationToken }
                )
            ).StatusCode
        );
        using var check = factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<PortalDbContext>();
        var verified = await db.Users.SingleAsync(user => user.Email == email);
        Assert.Equal(UserStatuses.Active, verified.Status);
        Assert.True(verified.EmailConfirmed);
        Assert.NotNull(verified.EmailVerifiedAt);
    }

    [Fact]
    public async Task Declared_image_with_invalid_signature_is_rejected()
    {
        var authenticated = await AdminClient();
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("this is not a png"));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "fake.png");
        var response = await authenticated.PostAsync(
            $"/api/admin/evidence?relatedEntityType=KpiPeriod&relatedEntityId={Guid.NewGuid()}",
            content
        );
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Account_change_request_has_explicit_admin_decision_visible_to_client()
    {
        var authenticatedClient = factory.CreateClient();
        var session = await authenticatedClient.PostAsJsonAsync(
            "/api/auth/session",
            new { email = "client@digitmak.mk", password = "DigitMak!2026Client" }
        );
        var sessionJson = await session.Content.ReadFromJsonAsync<JsonElement>();
        authenticatedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            sessionJson.GetProperty("accessToken").GetString()
        );

        var created = await authenticatedClient.PostAsJsonAsync(
            "/api/account-change-requests/",
            new
            {
                requestType = "Organization",
                details = "Move the client account to another approved organization.",
            }
        );
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdJson = await created.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = createdJson.GetProperty("id").GetGuid();

        var admin = await AdminClient();
        var decided = await admin.PostAsJsonAsync(
            $"/api/admin/account-change-requests/{requestId}/decision",
            new { status = "Accepted", note = "Choose the new organization from your portal." }
        );
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);

        var mine = await authenticatedClient.GetFromJsonAsync<JsonElement[]>(
            "/api/account-change-requests/my"
        );
        var item = Assert.Single(
            mine!,
            request => request.GetProperty("id").GetGuid() == requestId
        );
        Assert.Equal("Accepted", item.GetProperty("status").GetString());
        Assert.Equal(
            "Choose the new organization from your portal.",
            item.GetProperty("decisionNote").GetString()
        );
    }


    [Fact]
    public async Task Saved_service_content_is_visible_in_admin_inventory_and_public_catalogue()
    {
        var admin = await AdminClient();
        var slug = $"service-{Guid.NewGuid():N}";
        var response = await admin.PostAsJsonAsync(
            "/api/admin/services",
            new
            {
                slug,
                status = "Published",
                category = "Test",
                translations = new
                {
                    mk = new { title = "Тест услуга", description = "Еден ист опис" },
                },
            }
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var adminContent = await admin.GetFromJsonAsync<JsonElement>("/api/admin/services");
        Assert.Contains(
            adminContent.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("slug").GetString() == slug
        );

        foreach (var language in new[] { "mk", "en", "sq" })
        {
            var publicServices = await client.GetFromJsonAsync<JsonElement[]>(
                $"/api/public/services?language={language}"
            );
            var publicItem = Assert.Single(
                publicServices!,
                item => item.GetProperty("slug").GetString() == slug
            );
            Assert.Equal(
                "Тест услуга",
                publicItem.GetProperty("fields").GetProperty("title").GetString()
            );
            Assert.Equal(
                "Еден ист опис",
                publicItem.GetProperty("fields").GetProperty("description").GetString()
            );
        }
    }

    private async Task<HttpClient> AdminClient()
    {
        var authenticated = factory.CreateClient();
        var session = await authenticated.PostAsJsonAsync(
            "/api/auth/session",
            new { email = "admin@digitmak.mk", password = "DigitMak!2026Admin" }
        );
        var json = await session.Content.ReadFromJsonAsync<JsonElement>();
        authenticated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            json.GetProperty("accessToken").GetString()
        );
        return authenticated;
    }
}

public sealed class PortalFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(
            (_, configuration) =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["RateLimit:SensitivePermitLimit"] = "1000",
                        ["DemoAccount:Enabled"] = "true",
                    }
                )
        );
    }
}
