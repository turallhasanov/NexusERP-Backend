using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Audit;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Integration;

public class CatalogSyncService(
    IApplicationDbContext db, IStockWriter stockWriter, IKhazarCatalogClient khazar, IAuditLogger audit) : ICatalogSyncService
{
    private const int NameMaxLength = 200;
    private const int BarcodeMaxLength = 50;
    private const decimal MaxPrice = 9_999_999_999_999_999.99m;
    private const string DefaultCategoryName = "Khazar";
    private const string DefaultUnitName = "Ədəd";
    private const string DefaultUnitShortName = "əd.";
    private const int ImageUrlMaxLength = 500;

    public async Task<Result<ResetStockResponseDto>> ResetStockAsync(string companyId, CancellationToken cancellationToken)
    {
        // One UPDATE: no balance can be changed half-way, and a concurrent sale cannot slip a stale quantity back in.
        var reset = await db.Stocks
            .Where(s => s.CompanyId == companyId && s.Quantity != 0)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, 0m), cancellationToken);

        await audit.LogAsync(companyId, AuditModule.Inventory, AuditAction.Update,
            $"Bütün anbar stok qalıqları sıfırlandı ({reset} qalıq)", cancellationToken);

        return new ResetStockResponseDto(reset);
    }

    public async Task<Result<CatalogSyncResponseDto>> SyncAllAsync(string companyId, CancellationToken cancellationToken)
    {
        // The Khazar catalog belongs to the company connected to it; others must not import it as their own products.
        if (!await StorefrontAccess.IsEnabledAsync(db, companyId, cancellationToken))
            return Result<CatalogSyncResponseDto>.Failure(ErrorType.Validation, StorefrontAccess.NotConnectedMessage);

        var catalog = await khazar.GetCatalogAsync(cancellationToken);

        if (catalog.Items is not { } items)
            return Result<CatalogSyncResponseDto>.Failure(ErrorType.ServiceUnavailable,
                catalog.Error ?? "Khazar kataloqu oxunmadı.");

        var warehouse = await db.Warehouses
            .Where(w => w.CompanyId == companyId && w.IsActive)
            .OrderBy(w => w.Code.Length).ThenBy(w => w.Code)
            .FirstOrDefaultAsync(cancellationToken);

        if (warehouse is null)
            return Result<CatalogSyncResponseDto>.Failure(ErrorType.Validation, "Stoku yazmaq üçün əvvəlcə anbar yaradın.");

        var errors = new List<string>();
        var skipped = 0;
        var candidates = new List<KhazarCatalogItem>();

        foreach (var group in items.Where(i => i.IsActive).GroupBy(i => i.Sku?.Trim().ToUpperInvariant() ?? string.Empty))
        {
            var item = group.First();

            if (group.Key.Length == 0 || group.Key.Length > BarcodeMaxLength)
                Skip($"'{item.Name}': SKU boşdur və ya {BarcodeMaxLength} simvoldan uzundur.");
            else if (item.Price < 0 || item.Price > MaxPrice)
                Skip($"{group.Key}: qiymət yararsızdır.");
            else if (string.IsNullOrWhiteSpace(item.Name))
                Skip($"{group.Key}: ad boşdur.");
            else
                candidates.Add(item with { Sku = group.Key });
        }

        var skus = candidates.Select(c => c.Sku).ToList();
        var existing = (await db.Products
            .Where(p => p.CompanyId == companyId && skus.Contains(p.Barcode))
            .ToListAsync(cancellationToken))
            .ToDictionary(p => p.Barcode, StringComparer.OrdinalIgnoreCase);
        var stocks = await db.Stocks
            .Where(s => s.CompanyId == companyId && s.WarehouseId == warehouse.Id)
            .ToDictionaryAsync(s => s.ProductId, s => s.Quantity, cancellationToken);
        var categories = await db.Categories
            .Where(c => c.CompanyId == companyId)
            .ToListAsync(cancellationToken);

        Unit? unit = null;
        int created = 0, updated = 0, unchanged = 0, stockAdjusted = 0;

        foreach (var item in candidates)
        {
            var name = DisplayName(item);
            var price = Math.Round(item.Price, 2);
            var imageUrl = item.ImageUrl is { Length: > 0 and <= ImageUrlMaxLength } url ? url : null;

            if (existing.TryGetValue(item.Sku, out var product))
            {
                if (!product.IsActive)
                {
                    Skip($"{item.Sku}: NexusERP-də deaktiv məhsuldur.");
                    continue;
                }

                // A photo stored here (/uploads/products/...) is this system's own and stays; one that came from Khazar,
                // or none yet, follows Khazar.
                var imageChanged = imageUrl is not null && product.ImageUrl != imageUrl
                    && (product.ImageUrl is null || Uri.IsWellFormedUriString(product.ImageUrl, UriKind.Absolute));

                if (product.Name != name || product.SalePrice != price || imageChanged)
                {
                    product.Name = name;
                    product.SalePrice = price;
                    if (imageChanged)
                        product.ImageUrl = imageUrl;
                    await db.SaveChangesAsync(cancellationToken);
                    updated++;
                }
                else
                {
                    unchanged++;
                }
            }
            else
            {
                var category = await FindOrCreateCategoryAsync(companyId, categories, item.Category, cancellationToken);
                unit ??= await FindOrCreateUnitAsync(companyId, cancellationToken);

                var created_ = new Product
                {
                    CompanyId = companyId,
                    CategoryId = category.Id,
                    UnitId = unit.Id,
                    Name = name,
                    Barcode = item.Sku,
                    SalePrice = price,
                    ImageUrl = imageUrl
                };

                db.Products.Add(created_);
                await SequentialCode.AssignAndSaveAsync(
                    db, db.Products.Where(p => p.CompanyId == companyId).Select(p => p.Code), "MHS",
                    code => created_.Code = code, cancellationToken);
                product = created_;
                created++;
            }

            // Stock follows Khazar: the difference is booked through the same atomic writer sales and purchases use.
            var delta = item.Stock - stocks.GetValueOrDefault(product.Id);

            if (delta > 0)
                await stockWriter.IncreaseAsync(companyId, warehouse.Id, product.Id, delta, cancellationToken);
            else if (delta < 0 && !await stockWriter.TryDecreaseAsync(companyId, warehouse.Id, product.Id, -delta, cancellationToken))
            {
                Skip($"{item.Sku}: stok azaldıla bilmədi (qalıq dəyişib).");
                continue;
            }

            if (delta != 0)
                stockAdjusted++;
        }

        await audit.LogAsync(companyId, AuditModule.Inventory, AuditAction.Update,
            $"Khazar data inteqrasiyası: {created} yeni, {updated} yenilənib, {stockAdjusted} stok düzəlişi, {skipped} keçildi",
            cancellationToken);

        return new CatalogSyncResponseDto(items.Count, created, updated, unchanged, stockAdjusted, skipped, errors);

        void Skip(string message)
        {
            skipped++;
            errors.Add(message);
        }
    }

    /// <summary>Single-variant products keep Khazar's name; others get "Name (Color / Size)".</summary>
    private static string DisplayName(KhazarCatalogItem item)
    {
        var parts = new[] { item.Color?.Trim(), item.Size?.Trim() }
            .Where(p => !string.IsNullOrEmpty(p) && !p.Equals("Standart", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var name = parts.Count == 0 ? item.Name.Trim() : $"{item.Name.Trim()} ({string.Join(" / ", parts)})";

        return name.Length > NameMaxLength ? name[..NameMaxLength] : name;
    }

    private async Task<Category> FindOrCreateCategoryAsync(
        string companyId, List<Category> categories, string? name, CancellationToken cancellationToken)
    {
        var wanted = string.IsNullOrWhiteSpace(name) ? DefaultCategoryName : name.Trim();
        var match = categories.FirstOrDefault(c => c.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
            return match;

        var category = new Category { CompanyId = companyId, Name = wanted };
        db.Categories.Add(category);
        await SequentialCode.AssignAndSaveAsync(
            db, db.Categories.Where(c => c.CompanyId == companyId).Select(c => c.Code), "KTG",
            code => category.Code = code, cancellationToken);
        categories.Add(category);

        return category;
    }

    private async Task<Unit> FindOrCreateUnitAsync(string companyId, CancellationToken cancellationToken)
    {
        var units = await db.Units.Where(u => u.CompanyId == companyId).ToListAsync(cancellationToken);
        var unit = units.FirstOrDefault(u => u.Name.Equals(DefaultUnitName, StringComparison.OrdinalIgnoreCase))
            ?? units.FirstOrDefault();

        if (unit is not null)
            return unit;

        unit = new Unit { CompanyId = companyId, Name = DefaultUnitName, ShortName = DefaultUnitShortName };
        db.Units.Add(unit);
        await db.SaveChangesAsync(cancellationToken);

        return unit;
    }
}
