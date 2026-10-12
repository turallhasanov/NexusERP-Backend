using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NexusERP.Application.Common;

namespace NexusERP.Api.Middlewares;

/// <summary>
/// Rejects requests from users whose token carries no <c>companyId</c> claim.
/// Unauthenticated requests get the normal 401 challenge.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireCompanyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        if (string.IsNullOrEmpty(user.FindFirst(AppClaimTypes.CompanyId)?.Value))
        {
            context.Result = new ObjectResult(new ErrorResponse("Şirkət seçilməyib"))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
