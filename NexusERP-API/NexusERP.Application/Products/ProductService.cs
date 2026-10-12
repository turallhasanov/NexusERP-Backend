using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Products;

public class ProductService(IApplicationDbContext db) : IProductService
{
    private const string CodePrefix = "MHS";

    // Must match the column definitions in ProductConfiguration (prices are decimal(18,2)).
    private const int NameMaxLength = 200;
    private const int BarcodeMaxLength = 50;
    private const decimal MaxPrice = 9_999_999_999_999_999.99m;

    // 10^10 random in-store barcodes per company; a collision even once is unlikely, five in a row is not happening.
    private const int MaxBarcodeAttempts = 5;

    public async Task<Result<ItemsResponseDto<ProductDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken)
    {
        var items = await db.Products
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .OrderBy(p => p.Code.Length).ThenBy(p => p.Code)
            .Select(ProductDto.Projection)
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<ProductDto>(items);
    }

    public async Task<Result<ProductLookupResponseDto>> GetByBarcodeAsync(string companyId, string barcode, CancellationToken cancellationToken)
    {
        barcode = barcode.Trim();

        var product = await db.Products
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.Barcode == barcode)
            .Select(ProductDto.Projection)
            .FirstOrDefaultAsync(cancellationToken);

        return product is null
            ? Result<ProductLookupResponseDto>.Failure(ErrorType.NotFound, "Bu barkodla məhsul tapılmadı.")
            : new ProductLookupResponseDto(product);
    }

    public async Task<Result<ProductResponseDto>> CreateAsync(string companyId, CreateProductRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        var categoryId = request.CategoryId?.Trim();
        var unitId = request.UnitId?.Trim();
        var barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();

        if (string.IsNullOrEmpty(name))
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, "Məhsul adı tələb olunur.");

        if (name.Length > NameMaxLength)
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, $"Məhsul adı {NameMaxLength} simvoldan uzun ola bilməz.");

        if (string.IsNullOrEmpty(categoryId))
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, "Kateqoriya seçilməlidir.");

        if (string.IsNullOrEmpty(unitId))
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, "Ölçü vahidi seçilməlidir.");

        if (barcode?.Length > BarcodeMaxLength)
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, $"Barkod {BarcodeMaxLength} simvoldan uzun ola bilməz.");

        if (request.PurchasePrice is not { } purchasePrice)
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, "Alış qiyməti tələb olunur.");

        if (request.SalePrice is not { } salePrice)
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, "Satış qiyməti tələb olunur.");

        if (purchasePrice < 0 || salePrice < 0)
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, "Qiymət mənfi ola bilməz.");

        if (purchasePrice > MaxPrice || salePrice > MaxPrice)
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, "Qiymət həddindən artıq böyükdür.");

        // Scoped to the caller's company, so another company's category/unit looks exactly like a missing one.
        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.CompanyId == companyId, cancellationToken);

        if (category is null)
            return Result<ProductResponseDto>.Failure(ErrorType.NotFound, "Kateqoriya tapılmadı.");

        var unit = await db.Units
            .FirstOrDefaultAsync(u => u.Id == unitId && u.CompanyId == companyId, cancellationToken);

        if (unit is null)
            return Result<ProductResponseDto>.Failure(ErrorType.NotFound, "Ölçü vahidi tapılmadı.");

        if (barcode is not null)
        {
            if (await BarcodeExistsAsync(companyId, barcode, cancellationToken))
                return Result<ProductResponseDto>.Failure(ErrorType.Conflict, "Bu barkod artıq başqa məhsulda istifadə olunur.");
        }
        else
        {
            barcode = await GenerateBarcodeAsync(companyId, cancellationToken);
        }

        var product = new Product
        {
            CompanyId = companyId,
            CategoryId = category.Id,
            UnitId = unit.Id,
            Name = name,
            Barcode = barcode,
            // Rounded here so the response matches what decimal(18,2) stores.
            PurchasePrice = Math.Round(purchasePrice, 2),
            SalePrice = Math.Round(salePrice, 2)
        };

        db.Products.Add(product);
        await SequentialCode.AssignAndSaveAsync(
            db,
            db.Products.Where(p => p.CompanyId == companyId).Select(p => p.Code),
            CodePrefix,
            code => product.Code = code,
            cancellationToken);

        return ProductResponseDto.From(product, category, unit);
    }

    private Task<bool> BarcodeExistsAsync(string companyId, string barcode, CancellationToken cancellationToken) =>
        db.Products.AnyAsync(p => p.CompanyId == companyId && p.Barcode == barcode, cancellationToken);

    private async Task<string> GenerateBarcodeAsync(string companyId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var barcode = InStoreBarcode.Generate();

            if (!await BarcodeExistsAsync(companyId, barcode, cancellationToken))
                return barcode;

            if (attempt == MaxBarcodeAttempts)
                throw new InvalidOperationException($"Could not generate a free barcode for company '{companyId}'.");
        }
    }
}
