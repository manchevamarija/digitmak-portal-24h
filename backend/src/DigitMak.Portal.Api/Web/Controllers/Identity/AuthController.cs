using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DigitMak.Portal.Api.Infrastructure.Persistence;
using DigitMak.Portal.Api.Application;

namespace DigitMak.Portal.Api.Web.Controllers.Identity;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn,
    ITokenService tokens,
    PortalDbContext db,
    IConfiguration config,
    IHostEnvironment env
) : ControllerBase
{
    private const string RefreshCookie = "digitmak.refresh";

    [HttpPost("register")]
    public async Task<IResult> Register(RegisterRequest request)
    {
        if (!request.TermsAccepted)
            return Results.BadRequest(new { message = "Terms acceptance is required." });
        if (
            request.TermsVersion != LegalDocumentVersions.Terms
            || request.PrivacyVersion != LegalDocumentVersions.Privacy
        )
            return Results.BadRequest(
                new
                {
                    message = "The legal documents changed. Reload the page and review the current versions.",
                }
            );
        var developmentVerified = env.IsDevelopment();
        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.Phone,
            PreferredLanguage = request.PreferredLanguage,
            TermsAcceptedAt = DateTimeOffset.UtcNow,
            EmailConfirmed = developmentVerified,
            EmailVerifiedAt = developmentVerified ? DateTimeOffset.UtcNow : null,
            Status = developmentVerified ? UserStatuses.Active : UserStatuses.PendingVerification,
        };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return Results.ValidationProblem(
                result.Errors.ToDictionary(x => x.Code, x => new[] { x.Description })
            );
        await users.AddToRoleAsync(user, "Client");
        db.AuditLogs.Add(
            new AuditLog
            {
                ActorUserId = user.Id,
                Action = "LegalConsentAccepted",
                EntityType = nameof(AppUser),
                EntityId = user.Id.ToString(),
                MetadataJson = JsonSerializer.Serialize(
                    new
                    {
                        termsVersion = request.TermsVersion,
                        privacyVersion = request.PrivacyVersion,
                        acceptedAt = user.TermsAcceptedAt,
                        language = request.PreferredLanguage,
                    }
                ),
            }
        );
        db.AuditLogs.Add(
            new AuditLog
            {
                ActorUserId = user.Id,
                Action = "UserRegistered",
                EntityType = nameof(AppUser),
                EntityId = user.Id.ToString(),
            }
        );
        if (!env.IsDevelopment())
        {
            var token = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(await users.GenerateEmailConfirmationTokenAsync(user))
            );
            var root = (config["APP_PUBLIC_URL"] ?? "http://localhost:5173").TrimEnd('/');
            var link =
                $"{root}/verify-email?userId={Uri.EscapeDataString(user.Id.ToString())}&token={Uri.EscapeDataString(token)}";
            db.Notifications.Add(
                new Notification
                {
                    RecipientUserId = user.Id,
                    Type = "EmailVerification",
                    Subject = "Verify your DigitMak account",
                    Body =
                        $"<p>Confirm your account:</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Verify email</a></p>",
                }
            );
        }
        await db.SaveChangesAsync();
        return Results.Ok(new { user.Id, message = "Verify your email." });
    }

    [HttpPost("login")]
    [HttpPost("session")]
    [EnableRateLimiting("sensitive")]
    public async Task<IResult> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (
            user is null
            || user.Status != UserStatuses.Active
            || user.EmailVerifiedAt is null
            || !(await signIn.CheckPasswordSignInAsync(user, request.Password, true)).Succeeded
            || !user.EmailConfirmed
        )
        {
            db.AuditLogs.Add(
                new AuditLog
                {
                    ActorUserId = user?.Id,
                    Action = "LoginFailed",
                    EntityType = nameof(AppUser),
                    EntityId = user?.Id.ToString() ?? request.Email,
                }
            );
            await db.SaveChangesAsync();
            return Results.Unauthorized();
        }
        user.LastLoginAt = DateTimeOffset.UtcNow;
        db.AuditLogs.Add(
            new AuditLog
            {
                ActorUserId = user.Id,
                Action = "LoginSucceeded",
                EntityType = nameof(AppUser),
                EntityId = user.Id.ToString(),
            }
        );
        await db.SaveChangesAsync();
        var refresh = await tokens.CreateRefreshAsync(user);
        SetRefreshCookie(Response, refresh, env);
        return Results.Ok(
            new
            {
                accessToken = await tokens.CreateAsync(user),
                expiresIn = 1800,
                user = new
                {
                    user.Id,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.OrganizationId,
                    user.Status,
                    user.EmailVerifiedAt,
                    roles = await users.GetRolesAsync(user),
                },
            }
        );
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("sensitive")]
    public async Task<IResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue(RefreshCookie, out var raw))
            return Results.Unauthorized();
        var pair = await tokens.RotateRefreshAsync(raw);
        if (pair is null)
            return Results.Unauthorized();
        SetRefreshCookie(Response, pair.Value.Refresh, env);
        return Results.Ok(
            new { accessToken = await tokens.CreateAsync(pair.Value.User), expiresIn = 1800 }
        );
    }

    [HttpPost("logout")]
    public async Task<IResult> Logout()
    {
        var principal = User;
        if (Request.Cookies.TryGetValue(RefreshCookie, out var raw))
            await tokens.RevokeAsync(raw);
        if (
            Guid.TryParse(
                principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                out var userId
            )
        )
        {
            db.AuditLogs.Add(
                new AuditLog
                {
                    ActorUserId = userId,
                    Action = "Logout",
                    EntityType = nameof(AppUser),
                    EntityId = userId.ToString(),
                }
            );
            await db.SaveChangesAsync();
        }
        Response.Cookies.Delete(RefreshCookie);
        return Results.NoContent();
    }

    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IResult> Me()
    {
        var principal = User;
        return await users.GetUserAsync(principal) is { } user
            ? Results.Ok(
                new
                {
                    user.Id,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.OrganizationId,
                    user.Status,
                    user.EmailVerifiedAt,
                    roles = await users.GetRolesAsync(user),
                }
            )
            : Results.Unauthorized();
    }

    [HttpPost("verify-email")]
    [EnableRateLimiting("sensitive")]
    public async Task<IResult> VerifyEmail(VerifyEmailRequest request)
    {
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null || !TryDecodeToken(request.Token, out var token))
            return Results.BadRequest();
        var result = await users.ConfirmEmailAsync(user, token);
        if (result.Succeeded)
        {
            db.AuditLogs.Add(
                new AuditLog
                {
                    ActorUserId = user.Id,
                    Action = "EmailVerified",
                    EntityType = nameof(AppUser),
                    EntityId = user.Id.ToString(),
                }
            );
            await db.SaveChangesAsync();
            return Results.NoContent();
        }
        return Results.BadRequest(result.Errors);
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("sensitive")]
    public async Task<IResult> ForgotPassword(EmailRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is not null)
        {
            var token = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(user))
            );
            var root = (config["APP_PUBLIC_URL"] ?? "http://localhost:5173").TrimEnd('/');
            var link =
                $"{root}/reset-password?email={Uri.EscapeDataString(request.Email)}&token={Uri.EscapeDataString(token)}";
            db.Notifications.Add(
                new Notification
                {
                    RecipientUserId = user.Id,
                    Type = "PasswordReset",
                    Subject = "DigitMak password reset",
                    Body =
                        $"<p>Reset your password:</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Set a new password</a></p>",
                }
            );
            db.AuditLogs.Add(
                new AuditLog
                {
                    ActorUserId = user.Id,
                    Action = "PasswordResetRequested",
                    EntityType = nameof(AppUser),
                    EntityId = user.Id.ToString(),
                }
            );
            await db.SaveChangesAsync();
        }
        return Results.NoContent();
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("sensitive")]
    public async Task<IResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null || !TryDecodeToken(request.Token, out var token))
            return Results.BadRequest();
        var result = await users.ResetPasswordAsync(user, token, request.NewPassword);
        if (result.Succeeded)
        {
            db.AuditLogs.Add(
                new AuditLog
                {
                    ActorUserId = user.Id,
                    Action = "PasswordResetCompleted",
                    EntityType = nameof(AppUser),
                    EntityId = user.Id.ToString(),
                }
            );
            await db.SaveChangesAsync();
            return Results.NoContent();
        }
        return Results.BadRequest(result.Errors);
    }

    private static bool TryDecodeToken(string token, out string value)
    {
        try
        {
            value = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            return true;
        }
        catch (FormatException)
        {
            value = "";
            return false;
        }
    }

    private static void SetRefreshCookie(HttpResponse response, string token, IHostEnvironment env) =>
        response.Cookies.Append(
            RefreshCookie,
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = !env.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(14),
                Path = "/api/auth",
            }
        );
}
