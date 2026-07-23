using DigitMak.Portal.Api.Infrastructure.Persistence.Repositories;

namespace DigitMak.Portal.Api.Application;

public sealed class PublicContentService(IPublicContentRepository repository) : IPublicContentService
{
    private static readonly string[] SupportedLanguages = ["mk", "en", "sq"];

    public async Task<IReadOnlyList<PublicServiceModel>> GetServicesAsync(
        string? language,
        CancellationToken cancellationToken
    )
    {
        var services = await repository.ListPublishedServicesAsync(cancellationToken);
        if (services.Count == 0)
            return DefaultServices();

        var translationsByLanguage = await LoadTranslationsWithFallbackAsync(
            nameof(ServiceCatalogueItem),
            language,
            cancellationToken
        );

        return services
            .Select(service =>
                new PublicServiceModel(
                    service.Id,
                    service.Slug,
                    service.Category,
                    BuildFields(service.Id, translationsByLanguage)
                )
            )
            .ToArray();
    }

    public async Task<PublicPageModel?> GetPageAsync(
        string slug,
        string? language,
        CancellationToken cancellationToken
    )
    {
        var page = await repository.FindPublishedPageAsync(slug, cancellationToken);
        if (page is null)
            return null;

        var translationsByLanguage = await LoadTranslationsWithFallbackAsync(
            nameof(ContentPage),
            language,
            cancellationToken
        );

        return new PublicPageModel(page.Slug, BuildFields(page.Id, translationsByLanguage));
    }

    private async Task<IReadOnlyList<IReadOnlyList<Translation>>> LoadTranslationsWithFallbackAsync(
        string entityType,
        string? language,
        CancellationToken cancellationToken
    )
    {
        var requestedLanguage = NormalizeLanguage(language);
        var languagePriority = new[] { requestedLanguage, "mk", "en" }
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var result = new List<IReadOnlyList<Translation>>(languagePriority.Length);
        foreach (var candidate in languagePriority)
            result.Add(
                await repository.ListTranslationsAsync(entityType, candidate, cancellationToken)
            );
        return result;
    }

    private static IReadOnlyDictionary<string, string> BuildFields(
        Guid entityId,
        IReadOnlyList<IReadOnlyList<Translation>> translationsByLanguage
    )
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var translations in translationsByLanguage)
        foreach (var translation in translations.Where(item => item.EntityId == entityId))
            if (
                !fields.ContainsKey(translation.FieldName)
                && !string.IsNullOrWhiteSpace(translation.Value)
            )
                fields[translation.FieldName] = translation.Value;
        return fields;
    }

    private static string NormalizeLanguage(string? language) =>
        SupportedLanguages.Contains(language, StringComparer.Ordinal) ? language! : "mk";

    private static PublicServiceModel[] DefaultServices() =>
        [
            new(
                null,
                "ai-readiness",
                null,
                new Dictionary<string, string> { ["name"] = "AI Readiness" }
            ),
            new(
                null,
                "ai-act-compliance",
                null,
                new Dictionary<string, string> { ["name"] = "AI Act & Compliance" }
            ),
            new(
                null,
                "test-before-invest",
                null,
                new Dictionary<string, string> { ["name"] = "Test Before Invest" }
            ),
            new(
                null,
                "digital-roadmap",
                null,
                new Dictionary<string, string> { ["name"] = "Digital Roadmap" }
            ),
        ];
}
