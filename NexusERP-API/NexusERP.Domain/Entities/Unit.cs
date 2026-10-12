namespace NexusERP.Domain.Entities;

/// <summary>Unit of measure, e.g. Name "Ədəd" / ShortName "əd.", "Kiloqram" / "kq".</summary>
public class Unit
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
}
