using NexusERP.Application.Common;
using NexusERP.Application.Companies;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Admin;

/// <summary>
/// Body of POST /api/admin/tenants: the company and its first admin account. The account is created
/// confirmed, so the admin can sign in with this password straight away.
/// Fields are nullable so missing values reach our own validation instead of the framework's.
/// </summary>
public sealed record CreateTenantRequestDto(string? Name, string? Voen, string? AdminName, string? AdminEmail, string? AdminPassword);

/// <summary>Body of PUT /api/admin/tenants/{id}/status: { "status": "active" | "passive" | "suspended" }.</summary>
public sealed record UpdateTenantStatusRequestDto(CompanyStatus? Status);

/// <summary>One row of the tenant list.</summary>
public sealed record TenantDto(string Id, string Name, string? Voen, DateTime CreatedAt, CompanyStatus Status, int UserCount);

/// <summary>PUT /api/admin/tenants/{id}/status → { "ok": true, "tenant": { ... } }</summary>
public sealed record TenantResponseDto(TenantDto Tenant) : SuccessResponse;

public sealed record TenantUserDto(string Id, string Name, string Email, string Role, bool IsActive, bool IsEmailConfirmed, DateTime CreatedAt);

/// <summary>POST /api/admin/tenants → { "ok": true, "tenant": { ... }, "admin": { ... } }</summary>
public sealed record CreateTenantResponseDto(TenantDto Tenant, TenantUserDto Admin) : SuccessResponse;

/// <summary>Current record counts of a company (active and inactive rows alike).</summary>
public sealed record TenantCountsDto(int Products, int Warehouses, int Stores, int Customers, int Sales, int Purchases, int Expenses);

/// <summary>
/// A recent document of the company: <c>type</c> is "sale", "purchase" or "expense"; <c>status</c> is
/// "draft" or "approved" for documents and null for expenses (which have no draft step).
/// </summary>
public sealed record TenantActivityDto(string Type, string Title, decimal Amount, DateTime Date, string? Status);

public sealed record TenantDetailsDto(
    string Id,
    string Name,
    string? Voen,
    DateTime CreatedAt,
    CompanyStatus Status,
    string InviteCode,
    TenantCountsDto Counts,
    IReadOnlyList<TenantUserDto> Users,
    IReadOnlyList<TenantActivityDto> Activity);

/// <summary>GET /api/admin/tenants/{id} → { "ok": true, "tenant": { ... } }</summary>
public sealed record TenantDetailsResponseDto(TenantDetailsDto Tenant) : SuccessResponse;

/// <summary>
/// POST /api/admin/tenants/{id}/impersonate → { "ok": true, "token", "expiresAt", "company": { ... } }.
/// The token opens the company's regular endpoints read-only; the administrator's own token stays valid.
/// </summary>
public sealed record ImpersonationResponseDto(string Token, DateTime ExpiresAt, CompanyDto Company) : SuccessResponse;

public sealed record TenantRevenueDto(string Id, string Name, CompanyStatus Status, decimal Revenue);

/// <summary><c>status</c> is "healthy", or "slow" when the database took over a second to answer.</summary>
public sealed record SystemHealthDto(string Status, int DatabaseLatencyMs, DateTime CheckedAt);

/// <summary>
/// Platform-wide figures. Revenue is the total of approved sales (as in the profit-loss report) over all
/// companies; the last-30-days figures are by document date and creation date.
/// </summary>
public sealed record PlatformAnalyticsDto(
    int TotalTenants,
    int ActiveTenants,
    int PassiveTenants,
    int SuspendedTenants,
    int NewTenantsLast30Days,
    int TotalUsers,
    decimal TotalRevenue,
    decimal RevenueLast30Days,
    IReadOnlyList<TenantRevenueDto> TopTenants,
    SystemHealthDto Health);

/// <summary>GET /api/admin/analytics → { "ok": true, "analytics": { ... } }</summary>
public sealed record PlatformAnalyticsResponseDto(PlatformAnalyticsDto Analytics) : SuccessResponse;
