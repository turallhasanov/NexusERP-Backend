using NexusERP.Domain.Entities;

namespace NexusERP.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);

    /// <summary>
    /// Short-lived token that lets a platform administrator view <paramref name="companyId"/> as its admin
    /// would. "sub" stays the administrator's id and the impersonatedBy claim marks the token read-only.
    /// </summary>
    IssuedToken GenerateImpersonationToken(User superAdmin, string companyId);
}

public sealed record IssuedToken(string Token, DateTime ExpiresAt);
