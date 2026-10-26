using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Employees;

namespace NexusERP.Api.Controllers;

/// <summary>
/// Staff list (İnsan resursları). Every company user can read it (the dashboard and finance reports total the
/// payroll); hiring, leave, termination and the termination order are for admins and IT admins only, and deleting
/// an employee for IT admins only.
/// </summary>
[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class EmployeesController(IEmployeeService employeeService) : ApiControllerBase
{
    /// <summary>All employees, newest first. Leaves that ended before today are closed first (status back to active).</summary>
    [HttpGet]
    [ProducesResponseType<ItemsResponseDto<EmployeeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await employeeService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpPost]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<EmployeeResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await employeeService.CreateAsync(CurrentCompanyId, request, cancellationToken));

    /// <summary>Corrects the profile: name, position, department, hire date, salary and store.</summary>
    [HttpPut("{id}")]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<EmployeeResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateEmployeeRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await employeeService.UpdateAsync(CurrentCompanyId, id, request, cancellationToken));

    /// <summary>Sends the employee on leave from <c>startDate</c> to <c>endDate</c>; 409 once terminated.</summary>
    [HttpPost("{id}/vacation")]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<EmployeeResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SendOnVacation(string id, [FromBody] VacationRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await employeeService.SendOnVacationAsync(CurrentCompanyId, id, request, cancellationToken));

    /// <summary>"İşə qayıtdı": ends the leave; 409 when the employee is not on leave.</summary>
    [HttpPost("{id}/return")]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<EmployeeResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReturnFromVacation(string id, CancellationToken cancellationToken) =>
        ToActionResult(await employeeService.ReturnFromVacationAsync(CurrentCompanyId, id, cancellationToken));

    /// <summary>Ends employment on <c>terminationDate</c> (default today) and numbers the termination order.</summary>
    [HttpPost("{id}/terminate")]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<EmployeeResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Terminate(string id, [FromBody] TerminateEmployeeRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await employeeService.TerminateAsync(CurrentCompanyId, id, request, cancellationToken));

    /// <summary>
    /// Deletes the employee for good. IT admins ("itadmin") only: company admins, staff and platform admins viewing the
    /// company get 403.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = AppRoles.ITAdmin)]
    [ProducesResponseType<SuccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken) =>
        ToActionResult(await employeeService.DeleteAsync(CurrentCompanyId, id, cancellationToken));

    /// <summary>Data for the printable termination order (işdən çıxma əmri); 404 until the employee is terminated.</summary>
    [HttpGet("{id}/termination-document")]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<TerminationDocumentResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTerminationDocument(string id, CancellationToken cancellationToken) =>
        ToActionResult(await employeeService.GetTerminationDocumentAsync(CurrentCompanyId, id, cancellationToken));
}
