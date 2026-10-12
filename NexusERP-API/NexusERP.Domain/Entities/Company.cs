namespace NexusERP.Domain.Entities;

public class Company
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    /// <summary>VÖEN — Azerbaijani taxpayer identification number.</summary>
    public string? Voen { get; set; }

    /// <summary>
    /// Code staff enter to join this company (8 characters, unique). Generated on creation; only admins see it.
    /// </summary>
    public string InviteCode { get; set; } = string.Empty;
}
