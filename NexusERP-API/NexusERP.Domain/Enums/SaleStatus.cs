namespace NexusERP.Domain.Enums;

public enum SaleStatus
{
    /// <summary>Editable; stock is not affected yet.</summary>
    Draft = 1,

    /// <summary>Goods sold; stock has been decreased.</summary>
    Approved = 2,

    /// <summary>An online order the store cancelled; its stock has been returned. Excluded from every report.</summary>
    Cancelled = 3
}
