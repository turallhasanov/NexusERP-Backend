using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;

namespace NexusERP.Infrastructure.Authentication;

public class JwtTokenGenerator(IOptions<JwtSettings> options) : IJwtTokenGenerator
{
    private readonly JwtSettings _settings = options.Value;

    public string GenerateToken(User user)
    {
        var claims = new List<Claim>
        {
            new(AppClaimTypes.UserId, user.Id),
            new(AppClaimTypes.Email, user.Email),
            new(AppClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrEmpty(user.CompanyId))
            claims.Add(new Claim(AppClaimTypes.CompanyId, user.CompanyId));

        return Write(claims, DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes));
    }

    public IssuedToken GenerateImpersonationToken(User superAdmin, string companyId)
    {
        // Role "admin" so admin-only company pages open too; the token still cannot reach /api/admin.
        var claims = new List<Claim>
        {
            new(AppClaimTypes.UserId, superAdmin.Id),
            new(AppClaimTypes.Email, superAdmin.Email),
            new(AppClaimTypes.Role, AppRoles.Admin),
            new(AppClaimTypes.CompanyId, companyId),
            new(AppClaimTypes.ImpersonatedBy, superAdmin.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.ImpersonationExpiryMinutes);

        return new IssuedToken(Write(claims, expiresAt), expiresAt);
    }

    private string Write(IEnumerable<Claim> claims, DateTime expires)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
