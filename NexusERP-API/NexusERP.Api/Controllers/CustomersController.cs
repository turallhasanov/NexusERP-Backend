using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Customers;

namespace NexusERP.Api.Controllers;

[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class CustomersController(ICustomerService customerService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ItemsResponseDto<CustomerDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await customerService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpPost]
    [ProducesResponseType<CustomerResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await customerService.CreateAsync(CurrentCompanyId, request, cancellationToken));

    /// <summary>
    /// Removes the counterparty, or deactivates it when purchases or sales reference it (it then leaves the lists but stays
    /// on those documents). A system counterparty such as "Khazar Online Müştəri" is refused with 400. Company admins and
    /// IT admins only; staff get 403.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<CustomerDeleteResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken) =>
        ToActionResult(await customerService.DeleteAsync(CurrentCompanyId, id, cancellationToken));
}
