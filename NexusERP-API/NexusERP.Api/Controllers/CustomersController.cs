using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Customers;

namespace NexusERP.Api.Controllers;

[Route("api/customers")]
[Authorize, RequireCompany]
public class CustomersController(ICustomerService customerService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await customerService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await customerService.CreateAsync(CurrentCompanyId, request, cancellationToken));
}
