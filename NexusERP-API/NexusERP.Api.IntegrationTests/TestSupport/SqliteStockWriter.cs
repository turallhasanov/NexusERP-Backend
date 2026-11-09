using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Infrastructure.Persistence;

namespace NexusERP.Api.IntegrationTests.TestSupport;

/// <summary>
/// <see cref="IStockWriter"/> for the SQLite test host. The production StockWriter's increment is T-SQL
/// (UPDATE ... WITH (UPDLOCK, SERIALIZABLE) / IF @@ROWCOUNT), which SQLite cannot parse; this one keeps the same contract
/// with portable statements. TryDecreaseAsync is the production statement unchanged: the balance check sits in the
/// UPDATE's WHERE clause, so it never goes below zero.
/// </summary>
public sealed class SqliteStockWriter(AppDbContext db) : IStockWriter
{
    public async Task IncreaseAsync(string companyId, string warehouseId, string productId, decimal quantity, CancellationToken cancellationToken)
    {
        var updated = await db.Stocks
            .Where(s => s.CompanyId == companyId && s.WarehouseId == warehouseId && s.ProductId == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, x => x.Quantity + quantity), cancellationToken);

        if (updated == 0)
        {
            db.Stocks.Add(new Stock { CompanyId = companyId, WarehouseId = warehouseId, ProductId = productId, Quantity = quantity });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> TryDecreaseAsync(string companyId, string warehouseId, string productId, decimal quantity, CancellationToken cancellationToken)
    {
        var updated = await db.Stocks
            .Where(s => s.CompanyId == companyId && s.WarehouseId == warehouseId && s.ProductId == productId && s.Quantity >= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, x => x.Quantity - quantity), cancellationToken);

        return updated == 1;
    }
}
