using NexusERP.Application.Common;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Companies;

/// <summary>Fields are nullable so missing values reach our own validation instead of the framework's.</summary>
public sealed record CreateCompanyRequestDto(string? Name, string? Voen);

/// <summary>Body of POST /api/companies/join.</summary>
public sealed record JoinCompanyRequestDto(string? InviteCode);

public sealed record CompanyDto(string Id, string Name, string? Voen)
{
    public static CompanyDto From(Company company) => new(company.Id, company.Name, company.Voen);
}

/// <summary>GET /api/company → { "ok": true, "company": { ... } | null }</summary>
public sealed record CurrentCompanyResponseDto(CompanyDto? Company) : SuccessResponse;

/// <summary>
/// POST /api/company (create) and POST /api/companies/join → { "ok": true, "id", "name", "voen", "inviteCode",
/// "role", "token" }. <c>token</c> is a fresh JWT carrying the companyId claim and the user's new role
/// ("admin" for the creator, "staff" for someone who joined); the old token has neither.
/// </summary>
public sealed record CompanyResponseDto(string Id, string Name, string? Voen, string InviteCode, string Role, string Token) : SuccessResponse;

/// <summary>Company profile for its admins, including the code to share with staff.</summary>
public sealed record MyCompanyDto(string Id, string Name, string? Voen, string InviteCode, int MemberCount);

/// <summary>GET /api/companies/my-company → { "ok": true, "company": { ... } }</summary>
public sealed record MyCompanyResponseDto(MyCompanyDto Company) : SuccessResponse;
