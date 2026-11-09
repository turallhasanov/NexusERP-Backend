using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NexusERP.Application.Common;

namespace NexusERP.Api.Middlewares;

/// <summary>Marks an action or controller that an unapproved user may still call (sign-in state, company create/join).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowUnapprovedAttribute : Attribute;

/// <summary>
/// Global filter: a user who has not been approved (someone who joined a company with an invite code and waits for
/// its IT admin) gets 403 on every action except those marked <see cref="AllowUnapprovedAttribute"/>. Platform
/// admins and anonymous requests are not affected. The flag is read from the database on each request
/// (see JwtBearer OnTokenValidated), so approving a user takes effect at once without a new token.
/// </summary>
public sealed class RequireApprovedFilter : IAuthorizationFilter
{
    public const string UnapprovedItem = "UserUnapproved";

    public const string Message = "Hesabınız hələ təsdiqlənməyib. Rol təyini üçün İT / Sistem administratoru ilə əlaqə saxlayın.";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.Items.ContainsKey(UnapprovedItem))
            return;

        if (context.ActionDescriptor.EndpointMetadata.OfType<AllowUnapprovedAttribute>().Any())
            return;

        context.Result = new ObjectResult(new ErrorResponse(Message))
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
