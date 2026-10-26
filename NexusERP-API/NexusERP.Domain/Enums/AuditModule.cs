namespace NexusERP.Domain.Enums;

/// <summary>Area of the system an audited action belongs to; each has a department name (AuditLog.Department).</summary>
public enum AuditModule
{
    /// <summary>Maliyyə: purchases, expenses, cash registers.</summary>
    Finance = 1,

    /// <summary>İHR: employees.</summary>
    HR = 2,

    /// <summary>Anbar: products and warehouses.</summary>
    Inventory = 3,

    /// <summary>Şəbəkə: stores and sales (POS, wholesale, Khazar online).</summary>
    Retail = 4
}
