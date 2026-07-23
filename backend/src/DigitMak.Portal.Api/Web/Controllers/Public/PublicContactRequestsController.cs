using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DigitMak.Portal.Api.Web.Controllers.Public;

[ApiController]
[Route("api/public/contact-requests")]
public sealed class PublicContactRequestsController(IContactRequestService service) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("sensitive")]
    public async Task<IResult> Create(ContactRequestDto request, CancellationToken ct)
    {
        try
        {
            var item = await service.CreateAsync(request, ct);
            return Results.Created($"/api/admin/contact-requests/{item.Id}", item);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
