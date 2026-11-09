using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Audit;
using NexusERP.Application.Common;
using NexusERP.Application.Integration;
using NexusERP.Application.Products;
using NexusERP.Application.Tests.TestSupport;
using NexusERP.Domain.Enums;
using NexusERP.Infrastructure.Persistence;

namespace NexusERP.Application.Tests.Products;

/// <summary>Stock guards around products: no delete while stock remains, never a negative stock pushed to Khazar.</summary>
public sealed class ProductServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly Mock<IAuditLogger> _audit = new();
    private readonly Mock<IKhazarProductClient> _khazar = new();
    private readonly Mock<IProductImageFiles> _images = new();

    public ProductServiceTests() =>
        _khazar.Setup(k => k.PushProductAsync(It.IsAny<KhazarProductPush>(), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(KhazarProductResult.Ok);

    public void Dispose() => _database.Dispose();

    private ProductService CreateService(AppDbContext db) => new(db, _audit.Object, _khazar.Object, _images.Object);

    private static CreateProductRequestDto NewProduct(SeededCompany company, string name = "Yun palto", string? barcode = null,
        decimal? purchasePrice = 40m, decimal? salePrice = 89.999m, bool showOnWebsite = false) =>
        new(company.CategoryId, company.UnitId, name, barcode, purchasePrice, salePrice, showOnWebsite);

    // ---- Delete: stock guard and soft delete ----

    [Fact]
    public async Task DeleteAsync_is_refused_while_a_warehouse_holds_the_product()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var product = await TestData.ProductAsync(db, company, "MHS-001", "Köynək", "2000000000011");
        await TestData.StockAsync(db, company, product.Id, 4m);

        var result = await CreateService(db).DeleteAsync(company.CompanyId, product.Id, CancellationToken.None);

        result.ErrorType.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be("Anbarda bu məhsulun qalığı var, silinə bilməz.");

        await using var check = _database.CreateContext();
        (await check.Products.SingleAsync(p => p.Id == product.Id)).IsActive.Should().BeTrue();
        _audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteAsync_is_refused_for_a_negative_balance_too()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var product = await TestData.ProductAsync(db, company, "MHS-001", "Köynək", "2000000000011");
        await TestData.StockAsync(db, company, product.Id, -1m);

        var result = await CreateService(db).DeleteAsync(company.CompanyId, product.Id, CancellationToken.None);

        result.ErrorType.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task DeleteAsync_deactivates_a_product_with_an_empty_stock_row()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var product = await TestData.ProductAsync(db, company, "MHS-001", "Şərf", "2000000000028");
        await TestData.StockAsync(db, company, product.Id, 0m);

        var result = await CreateService(db).DeleteAsync(company.CompanyId, product.Id, CancellationToken.None);

        result.Value!.Deactivated.Should().BeTrue();
        await using var check = _database.CreateContext();
        (await check.Products.SingleAsync(p => p.Id == product.Id)).IsActive.Should().BeFalse();
        _audit.Verify(a => a.LogAsync(company.CompanyId, AuditModule.Inventory, AuditAction.Delete,
            "'MHS-001 · Şərf' məhsulu silindi (sənədlərdə istifadə olunduğu üçün deaktiv edildi)", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_removes_an_unused_product_and_its_photo_and_hides_it_on_khazar()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var product = await TestData.ProductAsync(db, company, "MHS-001", "Papaq", "2000000000035", showOnWebsite: true);
        product.ImageUrl = "/uploads/products/0123456789abcdef0123456789abcdef.png";
        await db.SaveChangesAsync();

        var result = await CreateService(db).DeleteAsync(company.CompanyId, product.Id, CancellationToken.None);

        result.Value!.Deactivated.Should().BeFalse();
        result.Value.Message.Should().Be("Məhsul uğurla silindi.");
        await using var check = _database.CreateContext();
        (await check.Products.AnyAsync(p => p.Id == product.Id)).Should().BeFalse();

        _images.Verify(i => i.TryDelete(product.ImageUrl), Times.Once);
        _khazar.Verify(k => k.PushProductAsync(
            It.Is<KhazarProductPush>(p => p.Sku == "2000000000035" && !p.IsPublished && p.Stock == 0),
            null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_reports_but_survives_a_failed_khazar_hide()
    {
        _khazar.Setup(k => k.PushProductAsync(It.IsAny<KhazarProductPush>(), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(KhazarProductResult.Failed("Khazar cavab vermir"));
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var product = await TestData.ProductAsync(db, company, "MHS-001", "Papaq", "2000000000035", showOnWebsite: true);

        var result = await CreateService(db).DeleteAsync(company.CompanyId, product.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Message.Should().Be("Məhsul uğurla silindi. Diqqət: məhsulu Khazar saytından gizlətmək alınmadı (Khazar cavab vermir).");
    }

    // ---- Storefront stock: whole units, never negative ----

    [Theory]
    [InlineData(new[] { 7.9, 2.0 }, 9)]     // all warehouses together, fractions floored
    [InlineData(new[] { 0.4 }, 0)]
    [InlineData(new[] { -3.5 }, 0)]         // a negative balance is pushed as 0, never below
    [InlineData(new[] { 5.0, -8.0 }, 0)]
    [InlineData(new double[0], 0)]          // no stock row at all
    public async Task The_stock_sent_to_khazar_is_floored_and_never_negative(double[] balances, int expectedStock)
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var product = await TestData.ProductAsync(db, company, "MHS-001", "Kaşmir sviter", "2000000000042");

        for (var i = 0; i < balances.Length; i++)
        {
            var warehouse = new Domain.Entities.Warehouse { CompanyId = company.CompanyId, Code = $"ANB-10{i}", Name = $"Anbar {i}" };
            db.Warehouses.Add(warehouse);
            await db.SaveChangesAsync();
            await TestData.StockAsync(db, company, product.Id, (decimal)balances[i], warehouse.Id);
        }

        var result = await CreateService(db).UpdateAsync(company.CompanyId, product.Id,
            new UpdateProductRequestDto(company.CategoryId, company.UnitId, "Kaşmir sviter", 30m, 75m, ShowOnWebsite: true),
            newImageUrl: null, removeImage: false, CancellationToken.None);

        result.Value!.Storefront.Status.Should().Be("synced");
        _khazar.Verify(k => k.PushProductAsync(
            It.Is<KhazarProductPush>(p => p.Sku == "2000000000042" && p.Stock == expectedStock && p.IsPublished && p.SalePrice == 75m),
            It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Create ----

    [Fact]
    public async Task CreateAsync_numbers_products_rounds_prices_generates_a_barcode_and_audits()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var service = CreateService(db);

        var first = await service.CreateAsync(company.CompanyId, NewProduct(company), imageUrl: null, CancellationToken.None);
        var second = await service.CreateAsync(company.CompanyId, NewProduct(company, "Dəri gödəkçə"), imageUrl: null, CancellationToken.None);

        first.Value!.Code.Should().Be("MHS-001");
        second.Value!.Code.Should().Be("MHS-002");
        first.Value.SalePrice.Should().Be(90.00m);
        first.Value.Barcode.Should().MatchRegex("^[0-9]{13}$");
        first.Value.Barcode.Should().NotBe(second.Value.Barcode);
        first.Value.Storefront.Should().Be(StorefrontSyncDto.NotShown);

        _khazar.VerifyNoOtherCalls();
        _audit.Verify(a => a.LogAsync(company.CompanyId, AuditModule.Inventory, AuditAction.Create,
            "'MHS-001 · Yun palto' məhsulu əlavə edildi", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(-1, 10, "Qiymət mənfi ola bilməz.")]
    [InlineData(10, -0.01, "Qiymət mənfi ola bilməz.")]
    public async Task CreateAsync_refuses_negative_prices(double purchasePrice, double salePrice, string error)
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);

        var result = await CreateService(db).CreateAsync(company.CompanyId,
            NewProduct(company, purchasePrice: (decimal)purchasePrice, salePrice: (decimal)salePrice), null, CancellationToken.None);

        result.ErrorType.Should().Be(ErrorType.Validation);
        result.Error.Should().Be(error);
        (await db.Products.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_requires_a_sale_price_and_a_name()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var service = CreateService(db);

        (await service.CreateAsync(company.CompanyId, NewProduct(company, salePrice: null), null, CancellationToken.None))
            .Error.Should().Be("Satış qiyməti tələb olunur.");
        (await service.CreateAsync(company.CompanyId, NewProduct(company, name: "  "), null, CancellationToken.None))
            .Error.Should().Be("Məhsul adı tələb olunur.");
    }

    [Fact]
    public async Task CreateAsync_refuses_a_barcode_another_product_has()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        await TestData.ProductAsync(db, company, "MHS-001", "Köynək", "KHZ-COAT-01");

        var result = await CreateService(db).CreateAsync(company.CompanyId, NewProduct(company, barcode: " KHZ-COAT-01 "), null, CancellationToken.None);

        result.ErrorType.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be("Bu barkod artıq başqa məhsulda istifadə olunur.");
    }

    [Fact]
    public async Task CreateAsync_saves_the_product_even_when_the_khazar_push_fails()
    {
        _khazar.Setup(k => k.PushProductAsync(It.IsAny<KhazarProductPush>(), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(KhazarProductResult.Failed("HTTP 503"));
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);

        var result = await CreateService(db).CreateAsync(company.CompanyId, NewProduct(company, showOnWebsite: true), null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Storefront.Should().Be(new StorefrontSyncDto("failed", "HTTP 503"));
        await using var check = _database.CreateContext();
        (await check.Products.SingleAsync()).ShowOnWebsite.Should().BeTrue();
    }

    // ---- Multi-tenant isolation: only a company connected to the storefront sends products to it ----

    [Fact]
    public async Task CreateAsync_does_not_send_to_the_storefront_for_a_company_without_a_connection()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db, "Başqa MMC", storefrontName: null);

        var result = await CreateService(db).CreateAsync(company.CompanyId, NewProduct(company, "BBB", showOnWebsite: true), null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Storefront.Should().Be(StorefrontSyncDto.NotShown);
        _khazar.Verify(k => k.PushProductAsync(It.IsAny<KhazarProductPush>(), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        await using var check = _database.CreateContext();
        (await check.Products.SingleAsync()).ShowOnWebsite.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_cannot_turn_the_storefront_on_for_a_company_without_a_connection()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db, "Başqa MMC", storefrontName: null);
        var product = await TestData.ProductAsync(db, company, "MHS-001", "BBB", "2000000000011");

        var result = await CreateService(db).UpdateAsync(
            company.CompanyId, product.Id,
            new UpdateProductRequestDto(company.CategoryId, company.UnitId, "BBB", 10m, 20m, ShowOnWebsite: true),
            null, false, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _khazar.Verify(k => k.PushProductAsync(It.IsAny<KhazarProductPush>(), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        await using var check = _database.CreateContext();
        (await check.Products.SingleAsync()).ShowOnWebsite.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_sends_to_the_storefront_for_a_connected_company()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);

        var result = await CreateService(db).CreateAsync(company.CompanyId, NewProduct(company, "BBB", showOnWebsite: true), null, CancellationToken.None);

        result.Value!.Storefront.Status.Should().Be("synced");
        _khazar.Verify(k => k.PushProductAsync(It.IsAny<KhazarProductPush>(), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
