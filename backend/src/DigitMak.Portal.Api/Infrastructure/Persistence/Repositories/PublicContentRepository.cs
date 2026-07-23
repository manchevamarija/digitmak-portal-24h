using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;
using Microsoft.EntityFrameworkCore;

namespace DigitMak.Portal.Api.Infrastructure.Persistence.Repositories;

public sealed class PublicContentRepository(PortalDbContext db) : IPublicContentRepository
{
    public async Task<IReadOnlyList<ServiceCatalogueItem>> ListPublishedServicesAsync(
        CancellationToken cancellationToken
    ) =>
        await db
            .ServiceCatalogueItems.AsNoTracking()
            .Where(item => item.Status == "Published")
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<ContentPage?> FindPublishedPageAsync(string slug, CancellationToken cancellationToken) =>
        db
            .ContentPages.AsNoTracking()
            .SingleOrDefaultAsync(page => page.Slug == slug && page.Status == "Published", cancellationToken);

    public async Task<IReadOnlyList<Translation>> ListTranslationsAsync(
        string entityType,
        string language,
        CancellationToken cancellationToken
    ) =>
        await db
            .Translations.AsNoTracking()
            .Where(translation => translation.EntityType == entityType && translation.Language == language)
            .ToListAsync(cancellationToken);
}
