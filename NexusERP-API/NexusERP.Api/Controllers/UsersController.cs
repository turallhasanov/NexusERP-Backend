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
public class UsersController(IRoleService roleService, ICompanyService companyService) : ApiControllerBase
{
    /// <summary>Users of the company with name, e-mail, role, status (isActive) and creation date.</summary>
    [HttpGet]
    [ProducesResponseType<ItemsResponseDto<CompanyUserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await companyService.GetUsersAsync(CurrentCompanyId, cancellationToken));

    /// <summary>
    /// Deactivates ({ "isActive": false }) or reactivates a user. A deactivated user cannot sign in and their current
    /// token is refused with 401 "Hesabınız deaktiv edilib." on the next request. IT admins only; you cannot change
    /// your own status.
    /// </summary>
    [HttpPatch("{userId}/status")]
    [Authorize(Roles = AppRoles.ITAdmin)]
    [ProducesResponseType<CompanyUserResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(string userId, [FromBody] SetUserStatusRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await companyService.SetUserActiveAsync(CurrentUserId, CurrentCompanyId, userId, request, cancellationToken));

    /// <summary>Gives the user one of the company's custom roles ({ "roleId": null } takes it away).</summary>
    [HttpPost("{userId}/assign-role")]
    [ProducesResponseType<CompanyUserResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole(string userId, [FromBody] AssignRoleRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await roleService.AssignAsync(CurrentCompanyId, userId, request, cancellationToken));
}
