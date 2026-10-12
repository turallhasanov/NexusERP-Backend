using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Application.Companies;

namespace NexusERP.Api.Controllers;

[Route("api/company")]
[Authorize]
public class CompanyController(ICompanyService companyService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        ToActionResult(await companyService.GetForUserAsync(CurrentUserId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await companyService.CreateAsync(CurrentUserId, request, cancellationToken));
}
