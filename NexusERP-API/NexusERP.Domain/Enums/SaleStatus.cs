namespace NexusERP.Domain.Enums;

public enum SaleStatus
{
    /// <summary>Editable; stock is not affected yet.</summary>
    Draft = 1,

    /// <summary>Goods sold; stock has been decreased.</summary>
    Approved = 2
}
