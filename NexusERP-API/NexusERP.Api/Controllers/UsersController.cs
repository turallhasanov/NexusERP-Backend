using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Companies;
using NexusERP.Application.Roles;

namespace NexusERP.Api.Controllers;

/// <summary>Users of the caller's company. Admins and IT admins only; staff get 403.</summary>
[Authorize(Roles = AppRoles.CompanyManagers), RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class UsersController(IRoleService roleService) : ApiControllerBase
{
    /// <summary>Gives the user one of the company's custom roles ({ "roleId": null } takes it away).</summary>
    [HttpPost("{userId}/assign-role")]
    [ProducesResponseType<CompanyUserResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole(string userId, [FromBody] AssignRoleRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await roleService.AssignAsync(CurrentCompanyId, userId, request, cancellationToken));
}
