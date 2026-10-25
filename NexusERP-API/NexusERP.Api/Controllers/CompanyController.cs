using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Companies;

namespace NexusERP.Api.Controllers;

// Reachable as api/company (existing clients) and api/companies; only api/companies is shown in Swagger.
// Declaring routes here replaces the inherited api/[controller] route, so both are listed.
[Route("api/[controller]")]
[Route("api/companies")]
[Authorize]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
public class CompanyController(ICompanyService companyService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<CurrentCompanyResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        ToActionResult(await companyService.GetForUserAsync(CurrentUserId, cancellationToken));

    [HttpPost]
    [ProducesResponseType<CompanyResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await companyService.CreateAsync(CurrentUserId, request, cancellationToken));

    /// <summary>Joins the company with this invite code as staff; returns a new token with the companyId claim.</summary>
    [HttpPost("join")]
    [ProducesResponseType<CompanyResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Join([FromBody] JoinCompanyRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await companyService.JoinAsync(CurrentUserId, request, cancellationToken));

    /// <summary>Company profile with the invite code. Admins and IT admins only; staff get 403.</summary>
    [HttpGet("my-company")]
    [Authorize(Roles = AppRoles.CompanyManagers), RequireCompany]
    [ProducesResponseType<MyCompanyResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyCompany(CancellationToken cancellationToken) =>
        ToActionResult(await companyService.GetMyCompanyAsync(CurrentCompanyId, cancellationToken));

    /// <summary>Users of the company with their roles. Admins and IT admins only.</summary>
    [HttpGet("users")]
    [Authorize(Roles = AppRoles.CompanyManagers), RequireCompany]
    [ProducesResponseType<ItemsResponseDto<CompanyUserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken) =>
        ToActionResult(await companyService.GetUsersAsync(CurrentCompanyId, cancellationToken));

    /// <summary>Assigns admin, itadmin or staff to another user of the company. Admins and IT admins only.</summary>
    [HttpPut("users/{id}/role")]
    [Authorize(Roles = AppRoles.CompanyManagers), RequireCompany]
    [ProducesResponseType<CompanyUserResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserRole(string id, [FromBody] UpdateCompanyUserRoleRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await companyService.UpdateUserRoleAsync(CurrentUserId, CurrentCompanyId, id, request, cancellationToken));
}
