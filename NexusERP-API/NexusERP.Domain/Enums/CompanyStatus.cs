namespace NexusERP.Domain.Enums;

/// <summary>Whether a company (tenant) may use the system. Set by platform administrators.</summary>
public enum CompanyStatus
{
    /// <summary>Normal operation.</summary>
    Active = 1,

    /// <summary>Switched off (e.g. the company stopped using the system); its users cannot sign in.</summary>
    Passive = 2,

    /// <summary>Frozen by the platform (e.g. unpaid or under review); its users cannot sign in.</summary>
    Suspended = 3,

    /// <summary>
    /// Deleted by a platform admin. Kept in the database with all its records (documents, stock, finance) for
    /// integrity, but hidden from the admin portal; its users cannot sign in and nobody can join it.
    /// </summary>
    Deleted = 4
}
