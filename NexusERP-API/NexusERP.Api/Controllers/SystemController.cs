using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Application.SystemInfo;

namespace NexusERP.Api.Controllers;

[AllowAnonymous]
public class SystemController(ISystemInfoService systemInfoService, IWebHostEnvironment environment) : ApiControllerBase
{
    /// <summary>Version, environment and copyright for the splash screen; available before sign-in.</summary>
    [HttpGet("info")]
    [ProducesResponseType<SystemInfoResponseDto>(StatusCodes.Status200OK)]
    public IActionResult GetInfo() =>
        ToActionResult(systemInfoService.GetInfo(environment.EnvironmentName));
}
