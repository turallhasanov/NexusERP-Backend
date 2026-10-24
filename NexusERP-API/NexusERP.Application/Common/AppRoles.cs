namespace NexusERP.Application.Common;

/// <summary>Values of <c>User.Role</c> and of the JWT "role" claim.</summary>
public static class AppRoles
{
    /// <summary>Company administrator: the company's creator, or the first user a platform admin sets up.</summary>
    public const string Admin = "admin";

    /// <summary>Company employee who joined with an invite code.</summary>
    public const string Staff = "staff";

    /// <summary>
    /// Platform administrator: manages every company from /api/admin and belongs to none. Never assigned by
    /// sign-up; the account comes from the "SuperAdmin" configuration section.
    /// </summary>
    public const string SuperAdmin = "superadmin";
}
