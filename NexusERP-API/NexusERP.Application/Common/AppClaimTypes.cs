namespace NexusERP.Application.Common;

/// <summary>Claim names written into the JWT and read back by the API (inbound claim mapping is disabled).</summary>
public static class AppClaimTypes
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Role = "role";
    public const string CompanyId = "companyId";
}
