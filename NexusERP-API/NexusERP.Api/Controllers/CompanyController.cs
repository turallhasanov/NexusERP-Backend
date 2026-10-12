using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Companies;

namespace NexusERP.Api.Controllers;

// Reachable as api/company (existing clients) and api/companies.
[Route("api/company")]
[Route("api/companies")]
[Authorize]
public class CompanyController(ICompanyService companyService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        ToActionResult(await companyService.GetForUserAsync(CurrentUserId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await companyService.CreateAsync(CurrentUserId, request, cancellationToken));

    /// <summary>Joins the company with this invite code as staff; returns a new token with the companyId claim.</summary>
    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinCompanyRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await companyService.JoinAsync(CurrentUserId, request, cancellationToken));

    /// <summary>Company profile with the invite code. Admins only; staff get 403.</summary>
    [HttpGet("my-company")]
    [Authorize(Roles = CompanyService.AdminRole), RequireCompany]
    public async Task<IActionResult> GetMyCompany(CancellationToken cancellationToken) =>
        ToActionResult(await companyService.GetMyCompanyAsync(CurrentCompanyId, cancellationToken));
}
