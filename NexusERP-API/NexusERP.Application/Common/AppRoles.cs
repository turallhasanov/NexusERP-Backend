namespace NexusERP.Application.Common;

/// <summary>Values of <c>User.Role</c> and of the JWT "role" claim.</summary>
public static class AppRoles
{
    /// <summary>Company administrator, assigned by a company manager (or chosen for a company's first user by a platform admin).</summary>
    public const string Admin = "admin";

    /// <summary>
    /// Company IT department: the same management rights as <see cref="Admin"/> inside its own company
    /// (company settings, user roles, deleting and changing data). Given to whoever registers a company and,
    /// by default, to the first user a platform admin sets up. Never a platform role.
    /// </summary>
    public const string ITAdmin = "itadmin";

    /// <summary>Company employee who joined with an invite code.</summary>
    public const string Staff = "staff";

    /// <summary>
    /// Platform administrator: manages every company from /api/admin and belongs to none. Never assigned by
    /// sign-up; the account comes from the "SuperAdmin" configuration section.
    /// </summary>
    public const string SuperAdmin = "superadmin";

    /// <summary>
    /// For <c>[Authorize(Roles = ...)]</c> on company management endpoints (settings, user roles, deletes and
    /// updates): company admins and IT admins.
    /// </summary>
    public const string CompanyManagers = Admin + "," + ITAdmin;

    /// <summary>Roles a company user can hold, and so the roles a company manager may assign.</summary>
    public static readonly IReadOnlyList<string> CompanyRoles = [Admin, ITAdmin, Staff];

    /// <summary>Roles the first user of a company created by a platform admin may get.</summary>
    public static readonly IReadOnlyList<string> CompanyManagerRoles = [Admin, ITAdmin];

    public static bool IsCompanyManager(string? role) => role is Admin or ITAdmin;
}
