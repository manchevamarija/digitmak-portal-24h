using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;
using Microsoft.AspNetCore.Mvc;

namespace DigitMak.Portal.Api.Web.Controllers.Public;

[ApiController]
[Route("api/public/pages")]
public sealed class PublicPagesController(IPublicContentService service) : ControllerBase
{
    [HttpGet("{slug}")]
    public async Task<IResult> GetPage(string slug, string? language, CancellationToken ct)
    {
        var page = await service.GetPageAsync(slug, language, ct);
        return page is null ? Results.NotFound() : Results.Ok(page);
    }
}
