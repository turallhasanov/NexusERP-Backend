using NexusERP.Application.Common;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Companies;

/// <summary>Fields are nullable so missing values reach our own validation instead of the framework's.</summary>
public sealed record CreateCompanyRequestDto(string? Name, string? Voen);

public sealed record CompanyDto(string Id, string Name, string? Voen)
{
    public static CompanyDto From(Company company) => new(company.Id, company.Name, company.Voen);
}

/// <summary>GET /api/company → { "ok": true, "company": { ... } | null }</summary>
public sealed record CurrentCompanyResponseDto(CompanyDto? Company) : SuccessResponse;

/// <summary>
/// POST /api/company → { "ok": true, "id", "name", "voen", "token" }.
/// <c>token</c> is a fresh JWT carrying the new companyId claim; the old token does not have it.
/// </summary>
public sealed record CompanyResponseDto(string Id, string Name, string? Voen, string Token) : SuccessResponse;
