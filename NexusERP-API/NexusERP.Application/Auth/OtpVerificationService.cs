using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Auth;

public enum OtpIssueStatus
{
    Sent,

    /// <summary>A code for this address and purpose was sent less than the cooldown ago; nothing sent.</summary>
    Cooldown,

    /// <summary>The mail server did not take the message; the new code was discarded.</summary>
    SendFailed
}

public readonly record struct OtpIssueResult(OtpIssueStatus Status, int RetryAfterSeconds = 0);

public enum OtpCheckResult
{
    Valid,
    Invalid,

    /// <summary>No usable code: never sent, expired, already used, or replaced by a newer one.</summary>
    ExpiredOrMissing,

    /// <summary>This wrong guess used up the last attempt; the code is now burned.</summary>
    TooManyAttempts
}

public interface IOtpVerificationService
{
    Task<OtpIssueResult> IssueAsync(string email, OtpPurpose purpose, CancellationToken cancellationToken);
    Task<OtpCheckResult> ConsumeAsync(string email, OtpPurpose purpose, string code, CancellationToken cancellationToken);
}

/// <summary>
/// 6-digit e-mail codes. Only a hash is stored; a code lives 10 minutes, allows 5 wrong guesses, and a new
/// one can be requested once a minute. Issuing a code cancels any older unused code for the same purpose.
/// </summary>
public class OtpVerificationService(IApplicationDbContext db, IEmailSender emailSender) : IOtpVerificationService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    public const int MaxFailedAttempts = 5;

    public async Task<OtpIssueResult> IssueAsync(string email, OtpPurpose purpose, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var lastSentAt = await db.OtpCodes
            .Where(o => o.Email == email && o.Purpose == purpose)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => (DateTime?)o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastSentAt is { } sentAt && now - sentAt < ResendCooldown)
            return new OtpIssueResult(OtpIssueStatus.Cooldown, (int)Math.Ceiling((ResendCooldown - (now - sentAt)).TotalSeconds));

        await db.OtpCodes
            .Where(o => o.Email == email && o.Purpose == purpose && !o.IsUsed)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsUsed, true), cancellationToken);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var otp = new OtpCode { Email = email, Purpose = purpose, CreatedAt = now, ExpiresAt = now + Lifetime };
        otp.CodeHash = Hash(otp.Id, code);

        db.OtpCodes.Add(otp);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await emailSender.SendAsync(OtpEmails.Build(email, purpose, code, Lifetime), cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Nobody received this code, so it must not stay usable or block a retry with the cooldown.
            await db.OtpCodes
                .Where(o => o.Id == otp.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.IsUsed, true)
                    .SetProperty(o => o.CreatedAt, DateTime.MinValue), cancellationToken);

            return new OtpIssueResult(OtpIssueStatus.SendFailed);
        }

        return new OtpIssueResult(OtpIssueStatus.Sent, (int)ResendCooldown.TotalSeconds);
    }

    public async Task<OtpCheckResult> ConsumeAsync(string email, OtpPurpose purpose, string code, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var otp = await db.OtpCodes
            .AsNoTracking()
            .Where(o => o.Email == email && o.Purpose == purpose && !o.IsUsed && o.ExpiresAt > now)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp is null)
            return OtpCheckResult.ExpiredOrMissing;

        var expected = Convert.FromHexString(otp.CodeHash);
        var actual = Convert.FromHexString(Hash(otp.Id, code));

        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            // Counted in SQL, so parallel guesses cannot each see "attempts left" and exceed the limit.
            await db.OtpCodes
                .Where(o => o.Id == otp.Id && !o.IsUsed)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.FailedAttempts, o => o.FailedAttempts + 1)
                    .SetProperty(o => o.IsUsed, o => o.FailedAttempts + 1 >= MaxFailedAttempts), cancellationToken);

            return otp.FailedAttempts + 1 >= MaxFailedAttempts ? OtpCheckResult.TooManyAttempts : OtpCheckResult.Invalid;
        }

        // Compare-and-set: of two requests with the same right code, only one gets through.
        var consumed = await db.OtpCodes
            .Where(o => o.Id == otp.Id && !o.IsUsed)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsUsed, true), cancellationToken);

        return consumed == 1 ? OtpCheckResult.Valid : OtpCheckResult.ExpiredOrMissing;
    }

    private static string Hash(string otpId, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{otpId}:{code}")));
}
