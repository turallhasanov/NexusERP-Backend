namespace NexusERP.Domain.Enums;

public enum PurchaseStatus
{
    /// <summary>Editable; stock is not affected yet.</summary>
    Draft = 1,

    /// <summary>Goods received; stock has been increased.</summary>
    Approved = 2
}
