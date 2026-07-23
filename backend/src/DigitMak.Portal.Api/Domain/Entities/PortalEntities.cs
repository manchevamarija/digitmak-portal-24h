using Microsoft.AspNetCore.Identity;

namespace DigitMak.Portal.Api.Domain.Entities;

public class AppUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string PreferredLanguage { get; set; } = "mk";
    public string Status { get; set; } = UserStatuses.Active;
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public Guid? OrganizationId { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? TermsAcceptedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class UserStatuses
{
    public const string PendingVerification = "PendingVerification";
    public const string Active = "Active";
    public const string Inactive = "Inactive";

    public static bool IsValid(string status) =>
        status is PendingVerification or Active or Inactive;
}

public static class LegalDocumentVersions
{
    public const string Terms = "terms-2026-07-v1";
    public const string Privacy = "privacy-2026-07-v1";
}

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Organization : Entity
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "SME";
    public string? Sector { get; set; }
    public string? Municipality { get; set; }
    public string? Region { get; set; }
    public string? Website { get; set; }
    public int? EmployeeCount { get; set; }
    public string Status { get; set; } = "PendingApproval";
    public Guid CreatedByUserId { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

public class OrganizationMember : Entity
{
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public string MemberStatus { get; set; } = "Active";
    public bool IsPrimaryContact { get; set; }
}

public class SubscriptionInvitation : Entity
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
    public string TokenHash { get; set; } = "";
    public string Status { get; set; } = "Invited";
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
}

public class Subscription : Entity
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? InvitationId { get; set; }
    public string Status { get; set; } = "PendingPayment";
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? OfflinePaymentReference { get; set; }
    public string? PaymentNote { get; set; }
    public Guid? InvitedBy { get; set; }
    public Guid? ActivatedBy { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}

public class AccountChangeRequest : Entity
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
    public string RequestType { get; set; } = "Organization";
    public string Details { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string? DecisionNote { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}

public class ContactRequest : Entity
{
    public string OrganizationName { get; set; } = "";
    public string OrganizationType { get; set; } = "SME";
    public string? Sector { get; set; }
    public string? Municipality { get; set; }
    public string? Region { get; set; }
    public string? Website { get; set; }
    public string ContactName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string PreferredLanguage { get; set; } = "mk";
    public int? EmployeeCount { get; set; }
    public int? DigitalMaturityRating { get; set; }
    public string DmaCategory { get; set; } = "DIGITAL_READINESS";
    public string MainNeed { get; set; } = "";
    public string ChallengeDescription { get; set; } = "";
    public string? CurrentTools { get; set; }
    public string? CurrentDataSources { get; set; }
    public bool? UsesAi { get; set; }
    public string? AiUseCase { get; set; }
    public string? PrivacyConcerns { get; set; }
    public bool InterestedInAiActGuidance { get; set; }
    public string? TrainingNeeds { get; set; }
    public string? DesiredTimeline { get; set; }
    public string? PreferredConsultationFormat { get; set; }
    public bool ConsentToContact { get; set; }
    public bool PrivacyPolicyAccepted { get; set; }
    public string Status { get; set; } = "New";
    public Guid? AssignedTo { get; set; }
    public Guid? LinkedOrganizationId { get; set; }
}

public class Ticket : Entity
{
    public string TicketNumber { get; set; } = "";
    public Guid OrganizationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Category { get; set; } = "OTHER";
    public string Priority { get; set; } = "Normal";
    public string Status { get; set; } = "New";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid? AssignedAgentId { get; set; }
    public Guid? AssignedExpertId { get; set; }
    public string? FinalRecommendation { get; set; }
    public string? ReferralRecommendation { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}

public class TicketMessage : Entity
{
    public Guid TicketId { get; set; }
    public Guid SenderUserId { get; set; }
    public string MessageType { get; set; } = "ClientMessage";
    public string Body { get; set; } = "";
    public DateTimeOffset? EditedAt { get; set; }
}

public class TicketAttachment : Entity
{
    public Guid TicketId { get; set; }
    public Guid? MessageId { get; set; }
    public Guid FileId { get; set; }
    public Guid UploadedBy { get; set; }
}

public class Meeting : Entity
{
    public Guid OrganizationId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? AssignedUserId { get; set; }
    public Guid? TicketId { get; set; }
    public string Subject { get; set; } = "";
    public string Description { get; set; } = "";
    public string MeetingType { get; set; } = "Online";
    public string? Location { get; set; }
    public string? OnlineLink { get; set; }
    public string? RequestedTimeWindow { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public string Status { get; set; } = "Requested";
    public string? Notes { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
}

public class ServiceCatalogueItem : Entity
{
    public string Slug { get; set; } = "";
    public string Status { get; set; } = "Published";
    public string Category { get; set; } = "General";
}

public class ContentPage : Entity
{
    public string Slug { get; set; } = "";
    public string Status { get; set; } = "Draft";
}

public class Translation : Entity
{
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public string Language { get; set; } = "mk";
    public string FieldName { get; set; } = "";
    public string Value { get; set; } = "";
}

public class EvidenceFile : Entity
{
    public string RelatedEntityType { get; set; } = "";
    public Guid RelatedEntityId { get; set; }
    public Guid FileId { get; set; }
    public string? KpiCategory { get; set; }
    public string? ReportingPeriod { get; set; }
    public string? TemplateType { get; set; }
    public Guid CreatedBy { get; set; }
}

public class EvidenceTemplate : Entity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string RelatedEntityType { get; set; } = "Ticket";
    public string Description { get; set; } = "";
    public string RequiredMetadataJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
}

public class SystemSetting : Entity
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Description { get; set; }
}

public class Notification : Entity
{
    public Guid? RecipientUserId { get; set; }
    public string? RecipientEmail { get; set; }
    public string? Language { get; set; }
    public string Type { get; set; } = "";
    public string Channel { get; set; } = "Email";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string Status { get; set; } = "Queued";
    public DateTimeOffset? SentAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public bool IsRead { get; set; }
    public string? ActionUrl { get; set; }
}

public class RefreshToken : Entity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByHash { get; set; }
}

public class FileObject : Entity
{
    public string OriginalFilename { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Checksum { get; set; } = "";
    public string Visibility { get; set; } = "Private";
    public Guid UploadedBy { get; set; }
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorIp { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
