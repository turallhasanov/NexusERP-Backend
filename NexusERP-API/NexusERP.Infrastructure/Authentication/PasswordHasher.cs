using Microsoft.AspNetCore.Identity;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using IdentityPasswordHasher = Microsoft.AspNetCore.Identity.PasswordHasher<NexusERP.Domain.Entities.User>;

namespace NexusERP.Infrastructure.Authentication;

/// <summary>
/// ASP.NET Core Identity's hasher: PBKDF2 with HMAC-SHA512, a random salt per password and a versioned
/// format, so iteration counts can be raised later and old hashes still verify (and get rehashed).
/// </summary>
public class PasswordHasher : Application.Common.Interfaces.IPasswordHasher
{
    private readonly IdentityPasswordHasher _hasher = new();

    // The Identity hasher takes a user argument but does not use it.
    private static readonly User NoUser = new();

    public string Hash(string password) => _hasher.HashPassword(NoUser, password);

    public PasswordCheck Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(NoUser, passwordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheck.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
            _ => PasswordCheck.Failed
        };
}
