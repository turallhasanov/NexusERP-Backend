namespace NexusERP.Domain.Entities;

public class Company
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    /// <summary>VÖEN — Azerbaijani taxpayer identification number.</summary>
    public string? Voen { get; set; }
}
