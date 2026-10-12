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

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
        ToActionResult(await authService.GetCurrentUserAsync(CurrentUserId, cancellationToken));

    /// <summary>JWTs are stateless; the client discards its token. Kept so the frontend has a stable endpoint.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout() => Ok(new SuccessResponse());
}
