namespace NexusERP.Domain.Entities;

/// <summary>Cash register (kassa): a place money is held, with its running balance.</summary>
public class CashRegister
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>Auto-generated per company: KSA-001, KSA-002, ...</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Changed only by transactions, atomically in SQL; never negative.</summary>
    public decimal Balance { get; set; }

    public bool IsActive { get; set; } = true;
}
