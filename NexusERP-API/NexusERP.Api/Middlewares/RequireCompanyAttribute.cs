using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NexusERP.Application.Auth;
using NexusERP.Application.Common;
using NexusERP.Application.Companies;

namespace NexusERP.Api.Middlewares;

/// <summary>
/// Rejects requests from users whose token carries no <c>companyId</c> claim, or whose company is no longer
/// active. Unauthenticated requests get the normal 401 challenge.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireCompanyAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        var companyId = user.FindFirst(AppClaimTypes.CompanyId)?.Value;

        if (string.IsNullOrEmpty(companyId))
        {
            context.Result = new ObjectResult(new ErrorResponse("Şirkət seçilməyib"))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        // Platform administrators may look at a passive or suspended company; its own users may not.
        if (user.HasClaim(c => c.Type == AppClaimTypes.ImpersonatedBy))
            return;

        // A token outlives a status change, so the status is read on every request (a primary-key lookup).
        var companies = context.HttpContext.RequestServices.GetRequiredService<ICompanyService>();
        var status = await companies.GetStatusAsync(companyId, context.HttpContext.RequestAborted);

        // 401 rather than 403: the session is over, so the client signs out and the login form shows why.
        if (status is { } current && AccountRules.CompanyBlockedMessage(current) is { } blocked)
        {
            context.Result = new ObjectResult(new ErrorResponse(blocked))
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }
    }
}
