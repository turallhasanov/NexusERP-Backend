using Microsoft.AspNetCore.Mvc;
using NexusERP.Application.Common;

namespace NexusERP.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id of the authenticated user (the JWT "sub" claim). Only valid on [Authorize] actions.</summary>
    protected string CurrentUserId =>
        User.FindFirst(AppClaimTypes.UserId)?.Value
        ?? throw new InvalidOperationException("Authenticated user has no 'sub' claim.");

    protected IActionResult ToActionResult<T>(Result<T> result) where T : ApiResponse
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        var statusCode = result.ErrorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return StatusCode(statusCode, new ErrorResponse(result.Error!));
    }
}
