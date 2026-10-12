namespace NexusERP.Application.Common.Interfaces;

/// <summary>Concurrency-safe changes to <c>Stocks</c> balances. Runs inside the DbContext's current transaction.</summary>
public interface IStockWriter
{
    /// <summary>Adds <paramref name="quantity"/> to the balance, creating the stock row if it does not exist yet.</summary>
    Task IncreaseAsync(string companyId, string warehouseId, string productId, decimal quantity, CancellationToken cancellationToken);
}
