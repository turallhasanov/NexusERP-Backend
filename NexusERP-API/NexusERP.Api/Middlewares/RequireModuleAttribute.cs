using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;

namespace NexusERP.Api.Middlewares;

/// <summary>
/// Opens a module (a key of <see cref="AppPermissions.All"/>) to company admins and IT admins, and to a user whose
/// custom role lists that module. Anyone else gets 403. The role is read on each request, so editing a role or
/// assigning another one takes effect without a new token. Put it beside <c>[Authorize]</c> and <c>[RequireCompany]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireModuleAttribute(string module) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        // Anonymous requests get the normal 401 from [Authorize]; managers see every module.
        if (user.Identity?.IsAuthenticated != true || user.IsInRole(AppRoles.Admin) || user.IsInRole(AppRoles.ITAdmin))
            return;

        var userId = user.FindFirst(AppClaimTypes.UserId)?.Value;
        var db = context.HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();

        var permissions = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Join(db.CustomRoles, u => u.CustomRoleId, r => r.Id, (_, r) => r.Permissions)
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (permissions is not null && permissions.Contains(module))
            return;

        context.Result = new ObjectResult(new ErrorResponse("Bu bölməyə icazəniz yoxdur."))
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
