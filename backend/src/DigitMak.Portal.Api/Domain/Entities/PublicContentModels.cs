namespace DigitMak.Portal.Api.Domain.Entities;

public sealed record PublicServiceModel(
    Guid? Id,
    string Slug,
    string? Category,
    IReadOnlyDictionary<string, string> Fields
);

public sealed record PublicPageModel(string Slug, IReadOnlyDictionary<string, string> Fields);
