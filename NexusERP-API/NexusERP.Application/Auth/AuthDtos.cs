using NexusERP.Application.Common;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Auth;

/// <summary>Fields are nullable so missing values reach our own validation instead of the framework's.</summary>
public sealed record LoginRequestDto(string? Email, string? Password);

/// <summary>Body of POST /api/auth/register. Nullable so missing values reach our own validation.</summary>
public sealed record RegisterRequestDto(string? FullName, string? Email, string? Password);

/// <summary>Body of POST /api/auth/verify-email-otp.</summary>
public sealed record VerifyEmailOtpRequestDto(string? Email, string? Code);

/// <summary>Body of POST /api/auth/resend-otp and POST /api/auth/forgot-password.</summary>
public sealed record EmailRequestDto(string? Email);

/// <summary>Body of POST /api/auth/reset-password.</summary>
public sealed record ResetPasswordRequestDto(string? Email, string? Code, string? NewPassword);

public sealed record UserDto(string Id, string Name, string Email, string Role, string? CompanyId)
{
    public static UserDto From(User user) => new(user.Id, user.Name, user.Email, user.Role, user.CompanyId);
}

/// <summary>Login and e-mail verification: { "ok": true, "token": "...", "user": { ... } }</summary>
public sealed record AuthResponseDto(string Token, UserDto User) : SuccessResponse;

/// <summary>
/// Steps that send or check a code: { "ok": true, "email", "message", "resendAfterSeconds" }.
/// <c>resendAfterSeconds</c> is when the next code may be requested (0 = now); the client runs its timer from it.
/// </summary>
public sealed record CodeStepResponseDto(string Email, string Message, int ResendAfterSeconds) : SuccessResponse;

/// <summary>{ "ok": true, "user": { ... } }</summary>
public sealed record CurrentUserResponseDto(UserDto User) : SuccessResponse;
