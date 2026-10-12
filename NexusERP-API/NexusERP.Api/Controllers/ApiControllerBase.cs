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

    /// <summary>Company of the authenticated user (the JWT "companyId" claim). Only valid on [RequireCompany] actions.</summary>
    protected string CurrentCompanyId =>
        User.FindFirst(AppClaimTypes.CompanyId)?.Value
        ?? throw new InvalidOperationException("Authenticated user has no 'companyId' claim.");

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
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            ErrorType.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        return StatusCode(statusCode, new ErrorResponse(result.Error!));
    }
}
