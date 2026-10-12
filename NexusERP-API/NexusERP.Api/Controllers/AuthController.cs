using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Application.Auth;
using NexusERP.Application.Common;

namespace NexusERP.Api.Controllers;

[Route("api/auth")]
public class AuthController(IAuthService authService) : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.LoginAsync(request, cancellationToken));

    /// <summary>Creates an unconfirmed account and e-mails a 6-digit code. No token until the code is verified.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.RegisterAsync(request, cancellationToken));

    /// <summary>Confirms the e-mail with the registration code; returns the same token as login.</summary>
    [HttpPost("verify-email-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmailOtp([FromBody] VerifyEmailOtpRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.VerifyEmailOtpAsync(request, cancellationToken));

    /// <summary>New registration code; 429 within a minute of the previous one.</summary>
    [HttpPost("resend-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendOtp([FromBody] EmailRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.ResendOtpAsync(request, cancellationToken));

    /// <summary>E-mails a reset code if the account exists; the answer never says whether it does.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] EmailRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.ForgotPasswordAsync(request, cancellationToken));

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.ResetPasswordAsync(request, cancellationToken));

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
        ToActionResult(await authService.GetCurrentUserAsync(CurrentUserId, cancellationToken));

    /// <summary>JWTs are stateless; the client discards its token. Kept so the frontend has a stable endpoint.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout() => Ok(new SuccessResponse());
}
