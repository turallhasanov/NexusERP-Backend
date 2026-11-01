namespace NexusERP.Domain.Enums;

public enum PurchaseStatus
{
    /// <summary>Editable; stock is not affected yet.</summary>
    Draft = 1,

    /// <summary>Goods received; stock has been increased.</summary>
    Approved = 2,

    /// <summary>Withdrawn by a company manager; the stock it added has been taken back out. Excluded from reports.</summary>
    Cancelled = 3
}
