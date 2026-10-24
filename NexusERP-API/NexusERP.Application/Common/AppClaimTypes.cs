namespace NexusERP.Application.Common;

/// <summary>Claim names written into the JWT and read back by the API (inbound claim mapping is disabled).</summary>
public static class AppClaimTypes
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Role = "role";
    public const string CompanyId = "companyId";

    /// <summary>
    /// Only on impersonation tokens: id of the platform administrator viewing the company. Such tokens are
    /// read-only (see ImpersonationReadOnlyFilter) and are not blocked when the company is inactive.
    /// </summary>
    public const string ImpersonatedBy = "impersonatedBy";
}
