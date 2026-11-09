using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexusERP.Application.Common.Interfaces;

namespace NexusERP.Application.Integration;

/// <summary>
/// Keeps the Khazar store's stock in step with the warehouses after a stock change (purchase approved or cancelled).
/// It runs after the change is committed and must never undo or fail it.
/// </summary>
public interface IStorefrontStockSync
{
    /// <summary>
    /// Sends the current total stock of each product that is shown on the store. Products not shown are skipped.
    /// Never throws: failures are logged and swallowed.
    /// </summary>
    Task SyncAsync(string companyId, IReadOnlyCollection<string> productIds, CancellationToken cancellationToken);
}

public sealed class StorefrontStockSync(
    IApplicationDbContext db, IKhazarProductClient khazarProducts, ILogger<StorefrontStockSync> logger) : IStorefrontStockSync
{
    public async Task SyncAsync(string companyId, IReadOnlyCollection<string> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
            return;

        try
        {
            if (!await StorefrontAccess.IsEnabledAsync(db, companyId, cancellationToken))
                return;

            var products = await db.Products
                .AsNoTracking()
                .Where(p => p.CompanyId == companyId && p.IsActive && p.ShowOnWebsite && productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Barcode, p.Name, p.SalePrice, CategoryName = p.Category!.Name })
                .ToListAsync(cancellationToken);

            if (products.Count == 0)
                return;

            var ids = products.Select(p => p.Id).ToList();
            var totals = (await db.Stocks
                    .Where(s => s.CompanyId == companyId && ids.Contains(s.ProductId))
                    .Select(s => new { s.ProductId, s.Quantity })
                    .ToListAsync(cancellationToken))
                .GroupBy(s => s.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Quantity));

            foreach (var product in products)
            {
                // The store sells whole units from all warehouses together, and never a negative number.
                var stock = (int)Math.Clamp(Math.Floor(totals.GetValueOrDefault(product.Id)), 0m, int.MaxValue);

                try
                {
                    var result = await khazarProducts.PushProductAsync(
                        new KhazarProductPush(product.Barcode, product.Name, product.CategoryName, product.SalePrice, stock, true),
                        null, null, cancellationToken);

                    if (!result.Success)
                        logger.LogWarning("Khazar stock sync failed for {Sku} (stock {Stock}): {Reason}", product.Barcode, stock, result.Error);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Khazar stock sync failed for {Sku} (stock {Stock}).", product.Barcode, stock);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Khazar stock sync could not read the stock of {Count} product(s).", productIds.Count);
        }
    }
}
