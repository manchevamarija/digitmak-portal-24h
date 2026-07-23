using System.Security.Claims;

namespace DigitMak.Portal.Api.Application;

public interface IContactRequestService
{
    Task<ContactRequest> CreateAsync(ContactRequestDto request, CancellationToken ct);
}

public interface IFileScanner
{
    Task EnsureSafeAsync(Stream content, string filename, CancellationToken ct);
}

public interface ITicketService
{
    Task<IReadOnlyList<Ticket>> GetMineAsync(Guid userId, CancellationToken ct);
    Task<Ticket?> GetVisibleAsync(Guid ticketId, ClaimsPrincipal principal, CancellationToken ct);
    Task<Ticket?> CreateAsync(TicketRequest request, Guid userId, CancellationToken ct);
    Task<Ticket?> CreateForUserAsync(
        TicketRequest request,
        Guid userId,
        Guid actorUserId,
        Guid? expectedOrganizationId,
        CancellationToken ct
    );
    Task<IReadOnlyList<TicketMessage>> GetMessagesAsync(
        Guid ticketId,
        ClaimsPrincipal principal,
        CancellationToken ct
    );
    Task<TicketMessage?> AddMessageAsync(
        Guid ticketId,
        string body,
        string type,
        ClaimsPrincipal principal,
        CancellationToken ct
    );
}

public interface IMeetingService
{
    Task<IReadOnlyList<Meeting>> GetMineAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<Meeting>> GetScheduledByMeAsync(Guid userId, CancellationToken ct);
    Task<Meeting?> CreateAsync(MeetingRequest request, Guid actorUserId, Guid userId, CancellationToken ct);
}

/// <summary>
/// Keeps the "which services/pages are published, in which language" logic out of
/// PublicServicesController/PublicPagesController — those controllers become thin request/response
/// shims, and this can be unit-tested against a fake IPublicContentRepository.
/// </summary>
public interface IPublicContentService
{
    Task<IReadOnlyList<PublicServiceModel>> GetServicesAsync(
        string? language,
        CancellationToken cancellationToken
    );

    Task<PublicPageModel?> GetPageAsync(string slug, string? language, CancellationToken cancellationToken);
}
