using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using DigitMak.Portal.Api.Infrastructure.Persistence;

namespace DigitMak.Portal.Api.Application;

public interface ITokenService
{
    Task<string> CreateAsync(AppUser user);
    Task<string> CreateRefreshAsync(AppUser user);
    Task<(AppUser User, string Refresh)?> RotateRefreshAsync(string token);
    Task RevokeAsync(string token);
}

public class TokenService(IConfiguration config, UserManager<AppUser> users, PortalDbContext db)
    : ITokenService
{
    public async Task<string> CreateAsync(AppUser user)
    {
        var roles = await users.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("org", user.OrganizationId?.ToString() ?? ""),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        return new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                config["Jwt:Issuer"],
                config["Jwt:Audience"],
                claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            )
        );
    }

    public async Task<string> CreateRefreshAsync(AppUser user)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        db.RefreshTokens.Add(
            new RefreshToken
            {
                UserId = user.Id,
                TokenHash = Hash(raw),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(14),
            }
        );
        await db.SaveChangesAsync();
        return raw;
    }

    public async Task<(AppUser User, string Refresh)?> RotateRefreshAsync(string token)
    {
        var hash = Hash(token);
        var now = DateTimeOffset.UtcNow;
        var current = await db.RefreshTokens.SingleOrDefaultAsync(x =>
            x.TokenHash == hash && x.RevokedAt == null && x.ExpiresAt > now
        );
        if (current is null)
            return null;
        var user = await users.FindByIdAsync(current.UserId.ToString());
        if (
            user is null
            || user.Status != UserStatuses.Active
            || !user.EmailConfirmed
            || user.EmailVerifiedAt is null
        )
            return null;
        current.RevokedAt = now;
        var next = await CreateRefreshAsync(user);
        current.ReplacedByHash = Hash(next);
        await db.SaveChangesAsync();
        return (user, next);
    }

    public async Task RevokeAsync(string token)
    {
        var hash = Hash(token);
        var current = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash);
        if (current != null)
        {
            current.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
