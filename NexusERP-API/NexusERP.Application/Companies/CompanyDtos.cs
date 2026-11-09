using NexusERP.Application.Common;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Companies;

/// <summary>Fields are nullable so missing values reach our own validation instead of the framework's.</summary>
public sealed record CreateCompanyRequestDto(string? Name, string? Voen);

/// <summary>Body of POST /api/companies/join.</summary>
public sealed record JoinCompanyRequestDto(string? InviteCode);

public sealed record CompanyDto(string Id, string Name, string? Voen, string? StorefrontName = null)
{
    public static CompanyDto From(Company company) => new(company.Id, company.Name, company.Voen, company.StorefrontName);
}

/// <summary>GET /api/company → { "ok": true, "company": { ... } | null }</summary>
public sealed record CurrentCompanyResponseDto(CompanyDto? Company) : SuccessResponse;

/// <summary>
/// POST /api/company (create) and POST /api/companies/join → { "ok": true, "id", "name", "voen", "inviteCode",
/// "role", "token" }. <c>token</c> is a fresh JWT carrying the companyId claim and the user's new role
/// ("itadmin" for the creator, "staff" for someone who joined); the old token has neither.
/// </summary>
public sealed record CompanyResponseDto(string Id, string Name, string? Voen, string InviteCode, string Role, string Token, bool IsApproved) : SuccessResponse;

/// <summary>Company profile for its admins, including the code to share with staff.</summary>
public sealed record MyCompanyDto(string Id, string Name, string? Voen, string InviteCode, int MemberCount, string? StorefrontName = null);

/// <summary>GET /api/companies/my-company → { "ok": true, "company": { ... } }</summary>
public sealed record MyCompanyResponseDto(MyCompanyDto Company) : SuccessResponse;

/// <summary>One user of the company, as its managers see it. <c>Role</c> is the access level; <c>CustomRoleId</c>
/// and <c>CustomRoleName</c> are the company's own job role (null when none is given).</summary>
public sealed record CompanyUserDto(
    string Id, string Name, string Email, string Role, string? CustomRoleId, string? CustomRoleName, bool IsActive, DateTime CreatedAt, bool IsApproved)
{
    public static CompanyUserDto From(User user, string? customRoleName) =>
        new(user.Id, user.Name, user.Email, user.Role, user.CustomRoleId, customRoleName, user.IsActive, user.CreatedAt, user.IsApproved);
}

/// <summary>Body of PUT /api/companies/users/{id}/role: { "role": "admin" | "itadmin" | "staff" }.</summary>
public sealed record UpdateCompanyUserRoleRequestDto(string? Role);

/// <summary>Body of PATCH /api/users/{id}/status: { "isActive": true | false }.</summary>
public sealed record SetUserStatusRequestDto(bool? IsActive);

/// <summary>Body of POST /api/users/{id}/approve: the role the approved user gets ("admin", "itadmin" or "staff").</summary>
public sealed record ApproveUserRequestDto(string? Role);

/// <summary>PUT /api/companies/users/{id}/role → { "ok": true, "user": { ... } }</summary>
public sealed record CompanyUserResponseDto(CompanyUserDto User) : SuccessResponse;
