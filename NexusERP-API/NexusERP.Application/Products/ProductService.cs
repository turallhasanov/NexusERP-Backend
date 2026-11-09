using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Audit;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Application.Integration;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Products;

public class ProductService(
    IApplicationDbContext db,
    IAuditLogger audit,
    IKhazarProductClient khazarProducts,
    IProductImageFiles imageFiles) : IProductService
{
    private const string CodePrefix = "MHS";

    // Must match the column definitions in ProductConfiguration (prices are decimal(18,2)).
    private const int NameMaxLength = 200;
    private const int BarcodeMaxLength = 50;
    private const decimal MaxPrice = 9_999_999_999_999_999.99m;

    // 10^10 random in-store barcodes per company; a collision even once is unlikely, five in a row is not happening.
    private const int MaxBarcodeAttempts = 5;

    public async Task<Result<ItemsResponseDto<ProductDto>>> GetAllAsync(string companyId, string? q, string? categoryId, CancellationToken cancellationToken)
    {
        // Deleted products that documents still reference stay in the table as inactive; the catalog hides them.
        var query = db.Products
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.IsActive);

        var categoryFilter = categoryId?.Trim();

        if (!string.IsNullOrEmpty(categoryFilter))
        {
            if (!await db.Categories.AnyAsync(c => c.Id == categoryFilter && c.CompanyId == companyId, cancellationToken))
                return Result<ItemsResponseDto<ProductDto>>.Failure(ErrorType.NotFound, "Kateqoriya tapılmadı.");

            query = query.Where(p => p.CategoryId == categoryFilter);
        }

        // Matches name, code or barcode anywhere in the text; case follows the column collation (insensitive by default).
        var search = q?.Trim();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Name.Contains(search) || p.Code.Contains(search) || p.Barcode.Contains(search));

        var items = await query
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

    public async Task<Result<ProductResponseDto>> CreateAsync(string companyId, CreateProductRequestDto request, string? imageUrl, CancellationToken cancellationToken,
        IReadOnlyList<string>? galleryImageUrls = null)
    {
        var name = request.Name?.Trim();
        var categoryId = request.CategoryId?.Trim();
        var unitId = request.UnitId?.Trim();
        var barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();

        if (string.IsNullOrEmpty(name))
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, "Məhsul adı tələb olunur.");

        name = VariantName.Normalize(name);

        if (name.Length > NameMaxLength)
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, $"Məhsul adı {NameMaxLength} simvoldan uzun ola bilməz.");

        if (string.IsNullOrEmpty(categoryId))
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, "Kateqoriya seçilməlidir.");

        if (string.IsNullOrEmpty(unitId))
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, "Ölçü vahidi seçilməlidir.");

        if (await NameExistsAsync(companyId, name, exceptId: null, cancellationToken))
            return Result<ProductResponseDto>.Failure(ErrorType.Conflict, DuplicateNameMessage(name));

        if (barcode?.Length > BarcodeMaxLength)
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, $"Barkod {BarcodeMaxLength} simvoldan uzun ola bilməz.");

        // Optional: the real purchase price comes from the first approved purchase, which overwrites it.
        var purchasePrice = request.PurchasePrice ?? 0;

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
            barcode = await GenerateUniqueBarcodeAsync(companyId, cancellationToken);
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
            SalePrice = Math.Round(salePrice, 2),
            ImageUrl = imageUrl,
            // Only a company connected to the storefront can show products on it.
            ShowOnWebsite = request.ShowOnWebsite && await StorefrontAccess.IsEnabledAsync(db, companyId, cancellationToken)
        };

        db.Products.Add(product);
        await SequentialCode.AssignAndSaveAsync(
            db,
            db.Products.Where(p => p.CompanyId == companyId).Select(p => p.Code),
            CodePrefix,
            code => product.Code = code,
            cancellationToken);

        await audit.LogAsync(companyId, AuditModule.Inventory, AuditAction.Create,
            $"'{product.Code} · {product.Name}' məhsulu əlavə edildi", cancellationToken);

        // A new product is sent with its photo; a failure is reported, not fatal: the product is saved either way.
        var storefront = product.ShowOnWebsite
            ? await PushToStorefrontAsync(
                companyId, product, category.Name, isPublished: true, sendImage: true, cancellationToken, galleryImageUrls)
            : StorefrontSyncDto.NotShown;

        return ProductResponseDto.From(product, category, unit, storefront);
    }

    public async Task<Result<ProductResponseDto>> UpdateAsync(
        string companyId, string id, UpdateProductRequestDto request, string? newImageUrl, bool removeImage, CancellationToken cancellationToken,
        IReadOnlyList<string>? galleryImageUrls = null)
    {
        var product = await db.Products.FirstOrDefaultAsync(
            p => p.Id == id && p.CompanyId == companyId && p.IsActive, cancellationToken);

        if (product is null)
            return Result<ProductResponseDto>.Failure(ErrorType.NotFound, "Məhsul tapılmadı.");

        var name = request.Name?.Trim();
        var categoryId = request.CategoryId?.Trim();
        var unitId = request.UnitId?.Trim();

        if (string.IsNullOrEmpty(name))
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, "Məhsul adı tələb olunur.");

        name = VariantName.Normalize(name);

        if (name.Length > NameMaxLength)
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, $"Məhsul adı {NameMaxLength} simvoldan uzun ola bilməz.");

        if (string.IsNullOrEmpty(categoryId))
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, "Kateqoriya seçilməlidir.");

        if (string.IsNullOrEmpty(unitId))
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, "Ölçü vahidi seçilməlidir.");

        // Only a rename is checked, so a product that already shares its name with another can still be edited.
        if (!string.Equals(name, product.Name, StringComparison.Ordinal)
            && await NameExistsAsync(companyId, name, product.Id, cancellationToken))
            return Result<ProductResponseDto>.Failure(ErrorType.Conflict, DuplicateNameMessage(name));

        if (request.SalePrice is not { } salePrice)
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, "Satış qiyməti tələb olunur.");

        var purchasePrice = request.PurchasePrice ?? product.PurchasePrice;

        if (purchasePrice < 0 || salePrice < 0)
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, "Qiymət mənfi ola bilməz.");

        if (purchasePrice > MaxPrice || salePrice > MaxPrice)
            return Result<ProductResponseDto>.Failure(ErrorType.Validation, "Qiymət həddindən artıq böyükdür.");

        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.CompanyId == companyId, cancellationToken);

        if (category is null)
            return Result<ProductResponseDto>.Failure(ErrorType.NotFound, "Kateqoriya tapılmadı.");

        var unit = await db.Units
            .FirstOrDefaultAsync(u => u.Id == unitId && u.CompanyId == companyId, cancellationToken);

        if (unit is null)
            return Result<ProductResponseDto>.Failure(ErrorType.NotFound, "Ölçü vahidi tapılmadı.");

        var wasShown = product.ShowOnWebsite;
        var oldImageUrl = product.ImageUrl;

        product.Name = name;
        product.CategoryId = category.Id;
        product.UnitId = unit.Id;
        product.PurchasePrice = Math.Round(purchasePrice, 2);
        product.SalePrice = Math.Round(salePrice, 2);
        // Turning it off is always allowed; turning it on needs a storefront connection.
        product.ShowOnWebsite = request.ShowOnWebsite && await StorefrontAccess.IsEnabledAsync(db, companyId, cancellationToken);

        if (newImageUrl is not null)
            product.ImageUrl = newImageUrl;
        else if (removeImage)
            product.ImageUrl = null;

        await db.SaveChangesAsync(cancellationToken);

        // The replaced photo is no longer referenced.
        if (oldImageUrl is not null && oldImageUrl != product.ImageUrl)
            imageFiles.TryDelete(oldImageUrl);

        await audit.LogAsync(companyId, AuditModule.Inventory, AuditAction.Update,
            $"'{product.Code} · {product.Name}' məhsulu dəyişdirildi", cancellationToken);

        // The photo goes along when it is new, or when the product is shown on the store for the first time. A product
        // that stays shown without a new photo keeps the gallery Khazar already has.
        var storefront = product.ShowOnWebsite || wasShown
            ? await PushToStorefrontAsync(
                companyId, product, category.Name, product.ShowOnWebsite,
                sendImage: product.ShowOnWebsite && (newImageUrl is not null || !wasShown), cancellationToken,
                product.ShowOnWebsite ? galleryImageUrls : null)
            : StorefrontSyncDto.NotShown;

        return ProductResponseDto.From(product, category, unit, storefront);
    }

    // Extra photos are read into memory for the one request that sends them; a file that is gone is skipped.
    private async Task<List<KhazarGalleryImage>?> ReadGalleryAsync(IReadOnlyList<string>? urls, CancellationToken cancellationToken)
    {
        if (urls is not { Count: > 0 })
            return null;

        var gallery = new List<KhazarGalleryImage>();

        foreach (var url in urls)
        {
            await using var stream = imageFiles.OpenRead(url);

            if (stream is null)
                continue;

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            gallery.Add(new KhazarGalleryImage(Path.GetFileName(url), buffer.ToArray()));
        }

        return gallery;
    }

    /// <summary>Sends the product to the Khazar store (add/update when shown, hide when not). Never throws.</summary>
    private async Task<StorefrontSyncDto> PushToStorefrontAsync(
        string companyId, Product product, string? categoryName, bool isPublished, bool sendImage, CancellationToken cancellationToken,
        IReadOnlyList<string>? galleryImageUrls = null)
    {
        // The storefront is shared by all companies: another company's products never go there.
        if (!await StorefrontAccess.IsEnabledAsync(db, companyId, cancellationToken))
            return StorefrontSyncDto.NotShown;

        // The store sells what the warehouses hold: all warehouses together, whole units only.
        var total = await db.Stocks
            .Where(s => s.CompanyId == companyId && s.ProductId == product.Id)
            .SumAsync(s => (decimal?)s.Quantity, cancellationToken) ?? 0m;
        var stock = (int)Math.Clamp(Math.Floor(total), 0m, int.MaxValue);

        Stream? image = sendImage && product.ImageUrl is not null ? imageFiles.OpenRead(product.ImageUrl) : null;
        var gallery = await ReadGalleryAsync(galleryImageUrls, cancellationToken);

        try
        {
            var result = await khazarProducts.PushProductAsync(
                new KhazarProductPush(product.Barcode, product.Name, categoryName, product.SalePrice, stock, isPublished)
                {
                    Gallery = gallery,
                },
                image,
                image is null ? null : Path.GetFileName(product.ImageUrl),
                cancellationToken);

            if (!result.Success)
                return new StorefrontSyncDto("failed", result.Error);

            return new StorefrontSyncDto(isPublished ? "synced" : "hidden");
        }
        finally
        {
            if (image is not null)
                await image.DisposeAsync();
        }
    }

    /// <summary>
    /// Refused (409) while any warehouse holds the product. A product that purchases, sales or stock rows still
    /// reference is deactivated instead of removed, so those documents keep it.
    /// </summary>
    public async Task<Result<ProductDeleteResponseDto>> DeleteAsync(string companyId, string id, CancellationToken cancellationToken)
    {
        var product = await db.Products.FirstOrDefaultAsync(
            p => p.Id == id && p.CompanyId == companyId && p.IsActive, cancellationToken);

        if (product is null)
            return Result<ProductDeleteResponseDto>.Failure(ErrorType.NotFound, "Məhsul tapılmadı.");

        if (await db.Stocks.AnyAsync(s => s.ProductId == id && s.Quantity != 0, cancellationToken))
            return Result<ProductDeleteResponseDto>.Failure(ErrorType.Conflict, "Anbarda bu məhsulun qalığı var, silinə bilməz.");

        // Purchase and sale lines and stock rows keep restricted foreign keys to the product.
        var isReferenced =
            await db.Stocks.AnyAsync(s => s.ProductId == id, cancellationToken) ||
            await db.PurchaseItems.AnyAsync(i => i.ProductId == id, cancellationToken) ||
            await db.SaleItems.AnyAsync(i => i.ProductId == id, cancellationToken);

        if (isReferenced)
            product.IsActive = false;
        else
            db.Products.Remove(product);

        await db.SaveChangesAsync(cancellationToken);

        await audit.LogAsync(companyId, AuditModule.Inventory, AuditAction.Delete,
            isReferenced
                ? $"'{product.Code} · {product.Name}' məhsulu silindi (sənədlərdə istifadə olunduğu üçün deaktiv edildi)"
                : $"'{product.Code} · {product.Name}' məhsulu silindi",
            cancellationToken);

        if (isReferenced is false && product.ImageUrl is not null)
            imageFiles.TryDelete(product.ImageUrl);

        // A product that was on the store must come off it, or customers could still order what no longer exists here.
        var storefrontWarning = string.Empty;
        if (product.ShowOnWebsite)
        {
            var hidden = await PushToStorefrontAsync(companyId, product, null, isPublished: false, sendImage: false, cancellationToken);
            if (hidden.Status == "failed")
                storefrontWarning = $" Diqqət: məhsulu Khazar saytından gizlətmək alınmadı ({hidden.Message}).";
        }

        return isReferenced
            ? new ProductDeleteResponseDto(true, "Bu məhsula bağlı əməliyyatlar (alış/satış) olduğu üçün silinə bilməz, deaktiv edildi." + storefrontWarning)
            : new ProductDeleteResponseDto(false, "Məhsul uğurla silindi." + storefrontWarning);
    }

    public async Task<Result<GeneratedBarcodeResponseDto>> GenerateBarcodeAsync(string companyId, CancellationToken cancellationToken) =>
        new GeneratedBarcodeResponseDto(await GenerateUniqueBarcodeAsync(companyId, cancellationToken));

    // Two live products with one name would reach the store as the same variant of the same product.
    private Task<bool> NameExistsAsync(string companyId, string name, string? exceptId, CancellationToken cancellationToken) =>
        db.Products.AnyAsync(
            p => p.CompanyId == companyId && p.IsActive && p.Name == name && p.Id != exceptId, cancellationToken);

    private static string DuplicateNameMessage(string name) =>
        $"'{name}' adlı məhsul artıq var. Yeni ölçü üçün adı \"Ad (Ölçü)\" formatında yazın, məsələn \"Usaq Hoodie (L)\".";

    private Task<bool> BarcodeExistsAsync(string companyId, string barcode, CancellationToken cancellationToken) =>
        db.Products.AnyAsync(p => p.CompanyId == companyId && p.Barcode == barcode, cancellationToken);

    private async Task<string> GenerateUniqueBarcodeAsync(string companyId, CancellationToken cancellationToken)
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
