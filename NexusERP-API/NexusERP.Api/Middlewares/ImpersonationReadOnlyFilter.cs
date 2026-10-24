using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NexusERP.Application.Common;

namespace NexusERP.Api.Middlewares;

/// <summary>
/// Global filter: impersonation tokens (those with the impersonatedBy claim) may only read. A platform
/// administrator viewing a company must not create, change or delete its data on the company's behalf.
/// </summary>
public sealed class ImpersonationReadOnlyFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;

        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
            return;

        if (!context.HttpContext.User.HasClaim(c => c.Type == AppClaimTypes.ImpersonatedBy))
            return;

        context.Result = new ObjectResult(new ErrorResponse("Şirkət baxış rejimindədir: dəyişiklik etmək mümkün deyil."))
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
