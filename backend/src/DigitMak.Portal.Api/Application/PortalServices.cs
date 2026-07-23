using System.Security.Claims;
using System.Text.Json;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Infrastructure.Persistence.Repositories;
using DigitMak.Portal.Api.Application;
using DigitMak.Portal.Api.Application.Realtime;

namespace DigitMak.Portal.Api.Application;

public sealed class ContactRequestService(IContactRequestRepository repository) : IContactRequestService
{
    public async Task<ContactRequest> CreateAsync(ContactRequestDto r, CancellationToken ct)
    {
        if (!r.ConsentToContact || !r.PrivacyPolicyAccepted)
            throw new ArgumentException("Consent and privacy acceptance are required.");
        if (r.DigitalMaturityRating is < 1 or > 5)
            throw new ArgumentException("Digital maturity rating must be between 1 and 5.");
        var dmaCategory = DmaCategoryMapping.Resolve(r);
        var item = new ContactRequest
        {
            OrganizationName = r.OrganizationName.Trim(),
            OrganizationType = r.OrganizationType.Trim(),
            Sector = r.Sector,
            Municipality = r.Municipality,
            Region = r.Region,
            Website = r.Website,
            ContactName = r.ContactName.Trim(),
            Email = r.Email.Trim(),
            Phone = r.Phone,
            PreferredLanguage = string.IsNullOrWhiteSpace(r.PreferredLanguage)
                ? "mk"
                : r.PreferredLanguage,
            EmployeeCount = r.EmployeeCount,
            DigitalMaturityRating = r.DigitalMaturityRating,
            DmaCategory = dmaCategory,
            MainNeed = r.MainNeed,
            ChallengeDescription = r.ChallengeDescription,
            CurrentTools = r.CurrentTools,
            CurrentDataSources = r.CurrentDataSources,
            UsesAi = r.UsesAi,
            AiUseCase = r.AiUseCase,
            PrivacyConcerns = r.PrivacyConcerns,
            InterestedInAiActGuidance = r.InterestedInAiActGuidance,
            TrainingNeeds = r.TrainingNeeds,
            DesiredTimeline = r.DesiredTimeline,
            PreferredConsultationFormat = r.PreferredConsultationFormat,
            ConsentToContact = true,
            PrivacyPolicyAccepted = true,
        };
        var confirmation = new Notification
        {
            RecipientEmail = item.Email,
            Language = string.IsNullOrWhiteSpace(r.PreferredLanguage) ? "mk" : r.PreferredLanguage,
            Type = "ContactRequestConfirmation",
            Subject = "Вашето барање за контакт е примено",
            Body =
                "<p>Вашето барање за контакт до DigitMak е примено. Нашиот тим ќе одговори во рок од два работни дена.</p>",
        };
        var adminUserIds = await repository.GetAdminUserIdsAsync(ct);
        var adminNotifications = adminUserIds
            .Select(adminId => new Notification
            {
                RecipientUserId = adminId,
                Type = "ContactRequestReceived",
                Subject = $"Ново барање за контакт: {item.OrganizationName}",
                Body =
                    $"<p>{item.ContactName} ({item.Email}) поднесе барање за контакт во име на „{item.OrganizationName}“.</p>",
                ActionUrl = "/admin?tab=contacts",
            })
            .ToList();
        var notifications = new List<Notification> { confirmation };
        notifications.AddRange(adminNotifications);
        var auditLog = new AuditLog
        {
            Action = "ContactRequestCreated",
            EntityType = nameof(ContactRequest),
            EntityId = item.Id.ToString(),
        };
        await repository.AddAsync(item, notifications, auditLog, ct);
        return item;
    }
}

public static class DmaCategoryMapping
{
    public static readonly string[] Values =
    [
        "DIGITAL_BUSINESS_STRATEGY",
        "DIGITAL_READINESS",
        "HUMAN_CENTRIC_DIGITALISATION",
        "DATA_MANAGEMENT",
        "AUTOMATION_AND_INTELLIGENCE",
        "GREEN_DIGITALISATION",
    ];

    public static bool IsValid(string? value) =>
        value is not null && Values.Contains(value, StringComparer.Ordinal);

    public static string Resolve(ContactRequestDto request)
    {
        if (!string.IsNullOrWhiteSpace(request.DmaCategory))
        {
            if (!IsValid(request.DmaCategory))
                throw new ArgumentException("Unsupported internal DMA category.");
            return request.DmaCategory;
        }

        if (
            !string.IsNullOrWhiteSpace(request.CurrentDataSources)
            || !string.IsNullOrWhiteSpace(request.PrivacyConcerns)
        )
            return "DATA_MANAGEMENT";
        if (request.UsesAi == true || request.MainNeed is "AI_USE_CASE" or "AI_ACT_COMPLIANCE")
            return "AUTOMATION_AND_INTELLIGENCE";
        return request.MainNeed switch
        {
            "FUNDING_AND_INVESTMENT" => "DIGITAL_BUSINESS_STRATEGY",
            "TRAINING_AND_SKILLS" => "HUMAN_CENTRIC_DIGITALISATION",
            "AUTOMATION_AND_INTELLIGENCE" => "AUTOMATION_AND_INTELLIGENCE",
            _ => "DIGITAL_READINESS",
        };
    }
}

public sealed class TicketService(
    ITicketRepository repository,
    IRealtimeTicketNotifier notifier,
    TicketPresenceTracker presence
) : ITicketService
{
    private static readonly HashSet<string> Categories =
    [
        "AI_READINESS",
        "AI_ACT_COMPLIANCE",
        "AI_USE_CASE",
        "DATA_GOVERNANCE",
        "AUTOMATION_AND_INTELLIGENCE",
        "DIGITALIZATION_ROADMAP",
        "TEST_BEFORE_INVEST",
        "TRAINING_AND_SKILLS",
        "FUNDING_AND_INVESTMENT",
        "REFERRAL",
        "OTHER",
    ];
    private static readonly HashSet<string> Priorities = ["Low", "Normal", "High", "Urgent"];

    public async Task<IReadOnlyList<Ticket>> GetMineAsync(Guid userId, CancellationToken ct) =>
        await repository.ListCreatedByAsync(userId, ct);

    public async Task<Ticket?> GetVisibleAsync(Guid id, ClaimsPrincipal p, CancellationToken ct)
    {
        var item = await repository.FindAsync(id, ct);
        if (item is null)
            return null;
        var userId = p.UserId();
        return
            item.CreatedByUserId == userId
            || item.AssignedAgentId == userId
            || item.AssignedExpertId == userId
            || p.IsInRole("Admin")
            ? item
            : null;
    }

    public Task<Ticket?> CreateAsync(TicketRequest r, Guid userId, CancellationToken ct) =>
        CreateForUserAsync(r, userId, userId, null, ct);

    public async Task<Ticket?> CreateForUserAsync(
        TicketRequest r,
        Guid userId,
        Guid actorUserId,
        Guid? expectedOrganizationId,
        CancellationToken ct
    )
    {
        var now = DateTimeOffset.UtcNow;
        var user = await repository.FindUserAsync(userId, ct);
        if (
            user?.OrganizationId is null
            || (expectedOrganizationId is not null && user.OrganizationId != expectedOrganizationId)
            || !await repository.HasPortalAccessAsync(userId, user.OrganizationId.Value, now, ct)
        )
            return null;
        if (
            !Categories.Contains(r.Category)
            || !Priorities.Contains(r.Priority ?? "Normal")
            || string.IsNullOrWhiteSpace(r.Title)
            || string.IsNullOrWhiteSpace(r.Description)
        )
            throw new ArgumentException("Invalid ticket category, priority, title or description.");
        var year = now.Year;
        var yearStart = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var yearEnd = yearStart.AddYears(1);
        var count = await repository.CountCreatedBetweenAsync(yearStart, yearEnd, ct);
        var item = new Ticket
        {
            TicketNumber = $"DM-{year}-{count + 1:0000}",
            OrganizationId = user.OrganizationId.Value,
            CreatedByUserId = userId,
            Category = r.Category,
            Title = r.Title.Trim(),
            Description = r.Description.Trim(),
            Priority = r.Priority ?? "Normal",
        };
        var notification = new Notification
        {
            RecipientUserId = userId,
            Type = "TicketCreated",
            Subject = $"Тикетот {item.TicketNumber} е креиран",
            Body = $"<p>Вашиот тикет <strong>{item.Title}</strong> е креиран.</p>",
            ActionUrl = $"/portal?tab=tickets&ticket={item.Id}",
        };
        var notifications = new List<Notification> { notification };
        if (item.Priority is "Urgent" or "High")
        {
            var staffUserIds = await repository.GetStaffUserIdsAsync(ct);
            var priorityLabel = item.Priority == "Urgent" ? "итен" : "висок";
            notifications.AddRange(
                staffUserIds.Select(staffUserId => new Notification
                {
                    RecipientUserId = staffUserId,
                    Type = "TicketCreated",
                    Subject = $"Нов тикет со {priorityLabel} приоритет: {item.TicketNumber}",
                    Body =
                        $"<p>Креиран е нов тикет <strong>{item.Title}</strong> со {priorityLabel} приоритет.</p>",
                    ActionUrl = $"/staff?tab=tickets&ticket={item.Id}",
                })
            );
        }
        var auditLog = new AuditLog
        {
            ActorUserId = actorUserId,
            Action = actorUserId == userId ? "TicketCreated" : "TicketCreatedOnBehalf",
            EntityType = nameof(Ticket),
            EntityId = item.Id.ToString(),
            MetadataJson =
                actorUserId == userId
                    ? null
                    : JsonSerializer.Serialize(
                        new { ClientUserId = userId, OrganizationId = user.OrganizationId.Value }
                    ),
        };
        await repository.AddTicketAsync(item, notifications, auditLog, ct);
        return item;
    }

    public async Task<IReadOnlyList<TicketMessage>> GetMessagesAsync(
        Guid id,
        ClaimsPrincipal p,
        CancellationToken ct
    )
    {
        if (await GetVisibleAsync(id, p, ct) is null)
            return [];
        var staff = p.IsInRole("Admin") || p.IsInRole("HelpDeskAgent") || p.IsInRole("Expert");
        return await repository.ListMessagesAsync(id, staff, ct);
    }

    public async Task<TicketMessage?> AddMessageAsync(
        Guid id,
        string body,
        string type,
        ClaimsPrincipal p,
        CancellationToken ct
    )
    {
        var ticket = await GetVisibleAsync(id, p, ct);
        if (ticket is null)
            return null;
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Message cannot be empty.");
        var item = new TicketMessage
        {
            TicketId = id,
            SenderUserId = p.UserId(),
            MessageType = type,
            Body = body.Trim(),
        };
        var recipients = new[] { ticket.CreatedByUserId, ticket.AssignedAgentId, ticket.AssignedExpertId }
            .Where(x => x is not null && x != p.UserId())
            .Select(x => x!.Value)
            .Distinct();
        var notifications = recipients
            .Where(recipient => !presence.IsPresent(id, recipient))
            .Select(recipient => new Notification
            {
                RecipientUserId = recipient,
                Type = "TicketMessageCreated",
                Subject = $"Нова порака на тикет {ticket.TicketNumber}",
                Body = $"<p>Има нова порака на тикетот <strong>{ticket.Title}</strong>.</p>",
                ActionUrl =
                    recipient == ticket.CreatedByUserId
                        ? $"/portal?tab=tickets&ticket={id}"
                        : $"/staff?tab=tickets&ticket={id}",
            })
            .ToArray();
        var auditLog = new AuditLog
        {
            ActorUserId = p.UserId(),
            Action = type + "Created",
            EntityType = nameof(TicketMessage),
            EntityId = item.Id.ToString(),
        };
        await repository.AddMessageAsync(item, notifications, auditLog, ct);
        await notifier.NotifyMessageCreatedAsync(id, item, ct);
        return item;
    }
}

public sealed class MeetingService(IMeetingRepository repository) : IMeetingService
{
    public async Task<IReadOnlyList<Meeting>> GetMineAsync(Guid userId, CancellationToken ct) =>
        await repository.ListRequestedByAsync(userId, ct);

    public async Task<IReadOnlyList<Meeting>> GetScheduledByMeAsync(Guid userId, CancellationToken ct) =>
        await repository.ListCreatedByAsync(userId, ct);

    public async Task<Meeting?> CreateAsync(MeetingRequest r, Guid actorUserId, Guid userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var user = await repository.FindUserAsync(userId, ct);
        if (
            user?.OrganizationId is null
            || !await repository.HasPortalAccessAsync(userId, user.OrganizationId.Value, now, ct)
        )
            return null;
        if (r.TicketId is not null && !await repository.OwnsTicketAsync(userId, r.TicketId.Value, ct))
            return null;
        var item = new Meeting
        {
            OrganizationId = user.OrganizationId.Value,
            RequestedByUserId = userId,
            CreatedByUserId = actorUserId,
            TicketId = r.TicketId,
            Subject = r.Subject,
            Description = r.Description,
            MeetingType = r.MeetingType,
            StartsAt = r.PreferredStart,
            EndsAt = r.PreferredEnd,
            RequestedTimeWindow = r.RequestedTimeWindow,
            Location = r.Location,
            OnlineLink = r.OnlineLink,
            Notes = r.Notes,
        };
        var scheduledByAdmin = actorUserId != userId;
        var notification = new Notification
        {
            RecipientUserId = userId,
            Type = scheduledByAdmin ? "MeetingScheduledByAdmin" : "MeetingRequested",
            Subject = scheduledByAdmin
                ? "Администраторот побара состанок со вас"
                : "Барањето за состанок е примено",
            Body = scheduledByAdmin
                ? $"<p>Администраторот побара состанок <strong>{item.Subject}</strong> со вас.</p>"
                : $"<p>Вашето барање за состанок <strong>{item.Subject}</strong> е примено.</p>",
            ActionUrl = "/portal?tab=meetings",
        };
        var auditLog = new AuditLog
        {
            ActorUserId = actorUserId,
            Action = scheduledByAdmin ? "MeetingScheduledByAdmin" : "MeetingRequested",
            EntityType = nameof(Meeting),
            EntityId = item.Id.ToString(),
        };
        await repository.AddAsync(item, notification, auditLog, ct);
        return item;
    }
}
