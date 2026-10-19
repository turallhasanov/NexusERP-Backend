using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Application.Auth;
using NexusERP.Application.Common;

namespace NexusERP.Api.Controllers;

public class AuthController(IAuthService authService) : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.LoginAsync(request, cancellationToken));

    /// <summary>Creates an unconfirmed account and e-mails a 6-digit code. No token until the code is verified.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<CodeStepResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.RegisterAsync(request, cancellationToken));

    /// <summary>Confirms the e-mail with the registration code; returns the same token as login.</summary>
    [HttpPost("verify-email-otp")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VerifyEmailOtp([FromBody] VerifyEmailOtpRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.VerifyEmailOtpAsync(request, cancellationToken));

    /// <summary>New registration code; 429 within a minute of the previous one.</summary>
    [HttpPost("resend-otp")]
    [AllowAnonymous]
    [ProducesResponseType<CodeStepResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ResendOtp([FromBody] EmailRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.ResendOtpAsync(request, cancellationToken));

    /// <summary>E-mails a reset code if the account exists; the answer never says whether it does.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType<CodeStepResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ForgotPassword([FromBody] EmailRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.ForgotPasswordAsync(request, cancellationToken));

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType<CodeStepResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.ResetPasswordAsync(request, cancellationToken));

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
        ToActionResult(await authService.GetCurrentUserAsync(CurrentUserId, cancellationToken));

    /// <summary>JWTs are stateless; the client discards its token. Kept so the frontend has a stable endpoint.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType<SuccessResponse>(StatusCodes.Status200OK)]
    public IActionResult Logout() => Ok(new SuccessResponse());
}
