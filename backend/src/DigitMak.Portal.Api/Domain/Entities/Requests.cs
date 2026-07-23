namespace DigitMak.Portal.Api.Domain.Entities;

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PreferredLanguage,
    string? Phone,
    bool TermsAccepted = true,
    string TermsVersion = LegalDocumentVersions.Terms,
    string PrivacyVersion = LegalDocumentVersions.Privacy
);

public record LoginRequest(string Email, string Password);

public record EmailRequest(string Email);

public record VerifyEmailRequest(string UserId, string Token);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);

public record OrganizationRequest(
    string Name,
    string Type,
    string? Sector,
    string? Municipality,
    string? Region,
    string? Website,
    int? EmployeeCount
);

public record ProfileRequest(
    string FirstName,
    string LastName,
    string? Phone,
    string PreferredLanguage
);

public record ContactRequestDto(
    string OrganizationName,
    string OrganizationType,
    string? Sector,
    string? Municipality,
    string? Region,
    string? Website,
    string ContactName,
    string Email,
    string? Phone,
    string PreferredLanguage,
    int? EmployeeCount,
    int? DigitalMaturityRating,
    string MainNeed,
    string ChallengeDescription,
    string? CurrentTools,
    string? CurrentDataSources,
    bool? UsesAi,
    string? AiUseCase,
    string? PrivacyConcerns,
    bool InterestedInAiActGuidance,
    string? TrainingNeeds,
    string? DesiredTimeline,
    string? PreferredConsultationFormat,
    bool ConsentToContact,
    bool PrivacyPolicyAccepted,
    string? DmaCategory = null
);

public record TicketRequest(string Category, string Title, string Description, string? Priority);

public record AdminTicketRequest(
    Guid UserId,
    Guid OrganizationId,
    string Category,
    string Title,
    string Description,
    string? Priority
);

public record TicketAssignmentRequest(Guid? AgentId, Guid? ExpertId);

public record TicketRecommendationRequest(
    string FinalRecommendation,
    string? ReferralRecommendation
);

public record MessageRequest(string Body);

public record MeetingRequest(
    string Subject,
    string Description,
    string MeetingType,
    Guid? TicketId,
    DateTimeOffset? PreferredStart,
    DateTimeOffset? PreferredEnd,
    string? RequestedTimeWindow,
    string? Location,
    string? OnlineLink,
    string? Notes
);

public record AdminMeetingRequest(Guid UserId, MeetingRequest Meeting);

public record MeetingDecisionRequest(
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    string? Location,
    string? OnlineLink,
    string? Notes,
    Guid? AssignedUserId
);

public record MeetingRescheduleRequest(
    DateTimeOffset? PreferredStart,
    DateTimeOffset? PreferredEnd,
    string? RequestedTimeWindow,
    string? Notes
);

public record SubscriptionInviteRequest(Guid UserId, Guid OrganizationId);

public record SubscriptionActivationRequest(string PaymentReference, string? PaymentNote);

public record AccountChangeRequestDto(string RequestType, string Details);

public record AccountChangeDecisionRequest(string Status, string? Note);

public record ApplyOrganizationChangeRequest(Guid OrganizationId);

public record ContactUpdateRequest(
    string Status,
    Guid? AssignedTo,
    Guid? LinkedOrganizationId,
    string? DmaCategory = null
);

public record UserUpdateRequest(string Status, string PreferredLanguage, string? Phone);

public record RolesRequest(string[] Roles);

public record ContentUpsertRequest(
    string Slug,
    string Status,
    string Category,
    Dictionary<string, Dictionary<string, string>> Translations
);

public record SettingRequest(string Value, string? Description);

public record EvidenceTemplateRequest(
    string Code,
    string Name,
    string RelatedEntityType,
    string Description,
    string[] RequiredMetadata,
    bool IsActive = true
);
