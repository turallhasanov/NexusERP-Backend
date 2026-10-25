using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Roles;

namespace NexusERP.Api.Controllers;

/// <summary>The company's custom job roles. Admins and IT admins only; staff get 403.</summary>
[Authorize(Roles = AppRoles.CompanyManagers), RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class RolesController(IRoleService roleService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ItemsResponseDto<RoleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await roleService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpPost]
    [ProducesResponseType<RoleResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await roleService.CreateAsync(CurrentCompanyId, request, cancellationToken));

    [HttpPut("{id}")]
    [ProducesResponseType<RoleResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateRoleRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await roleService.UpdateAsync(CurrentCompanyId, id, request, cancellationToken));

    /// <summary>Deletes a role nobody has (409 otherwise).</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType<SuccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken) =>
        ToActionResult(await roleService.DeleteAsync(CurrentCompanyId, id, cancellationToken));
}
