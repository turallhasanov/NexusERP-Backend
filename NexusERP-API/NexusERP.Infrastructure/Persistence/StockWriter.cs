using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common.Interfaces;

namespace NexusERP.Infrastructure.Persistence;

public class StockWriter(AppDbContext db) : IStockWriter
{
    /// <summary>
    /// Atomic upsert. A tracked read-modify-write would lose increments when two approvals touch the same
    /// product at once; here the increment happens in SQL, and UPDLOCK + SERIALIZABLE hold a key-range lock
    /// so two callers cannot both find the row missing and both insert it.
    /// </summary>
    public async Task IncreaseAsync(string companyId, string warehouseId, string productId, decimal quantity, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid().ToString();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [Stocks] WITH (UPDLOCK, SERIALIZABLE)
            SET [Quantity] = [Quantity] + {quantity}
            WHERE [CompanyId] = {companyId} AND [WarehouseId] = {warehouseId} AND [ProductId] = {productId};

            IF @@ROWCOUNT = 0
                INSERT INTO [Stocks] ([Id], [CompanyId], [WarehouseId], [ProductId], [Quantity])
                VALUES ({id}, {companyId}, {warehouseId}, {productId}, {quantity});
            """, cancellationToken);
    }

    /// <summary>
    /// The balance check is part of the UPDATE's WHERE clause, so check and subtract are one atomic
    /// statement: two tills selling the last units at once cannot both succeed and drive stock negative.
    /// </summary>
    public async Task<bool> TryDecreaseAsync(string companyId, string warehouseId, string productId, decimal quantity, CancellationToken cancellationToken)
    {
        var updated = await db.Stocks
            .Where(s => s.CompanyId == companyId && s.WarehouseId == warehouseId && s.ProductId == productId && s.Quantity >= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, x => x.Quantity - quantity), cancellationToken);

        return updated == 1;
    }
}
