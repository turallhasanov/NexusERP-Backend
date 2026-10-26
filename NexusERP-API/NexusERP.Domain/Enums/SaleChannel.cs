namespace NexusERP.Domain.Enums;

/// <summary>Where a sale came from.</summary>
public enum SaleChannel
{
    /// <summary>Entered in NexusERP: the sales form (wholesale) or POS checkout.</summary>
    Direct = 1,

    /// <summary>Retail (B2C) order from the Khazar online store, sent through POST /api/online-orders.</summary>
    Online = 2
}
