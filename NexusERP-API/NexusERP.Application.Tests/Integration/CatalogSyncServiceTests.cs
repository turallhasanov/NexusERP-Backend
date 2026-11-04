using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Audit;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Application.Integration;
using NexusERP.Application.Tests.TestSupport;
using NexusERP.Infrastructure.Persistence;

namespace NexusERP.Application.Tests.Integration;

/// <summary>
/// Khazar catalog pull ("Data Inteqrasiyası"): how a Khazar variant is named in NexusERP, and the stock guard when a
/// balance cannot be lowered. Khazar's client and the stock writer are mocked.
/// </summary>
public sealed class CatalogSyncServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly Mock<IKhazarCatalogClient> _khazar = new();
    private readonly Mock<IStockWriter> _stock = new();
    private readonly Mock<IAuditLogger> _audit = new();

    public CatalogSyncServiceTests() =>
        _stock.Setup(s => s.TryDecreaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    public void Dispose() => _database.Dispose();

    private CatalogSyncService CreateService(AppDbContext db) => new(db, _stock.Object, _khazar.Object, _audit.Object);

    private void Catalog(params KhazarCatalogItem[] items) =>
        _khazar.Setup(k => k.GetCatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new KhazarCatalogResult(items, null));

    private static KhazarCatalogItem Item(string sku, string name, string? size, string? color, int stock = 0, decimal price = 50m) =>
        new(sku, name, "Palto", size, color, price, stock, IsActive: true);

    [Theory]
    [InlineData("M", "Qara", "Yun palto (Qara / M)")]
    [InlineData("Standart", "Standart", "Yun palto")]  // single-variant products keep the plain name
    [InlineData("standart", "Bej", "Yun palto (Bej)")]  // "Standart" is skipped case-insensitively
    [InlineData(" L ", null, "Yun palto (L)")]
    [InlineData(null, "  ", "Yun palto")]
    public async Task A_variant_is_named_Name_Color_Size(string? size, string? color, string expected)
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        Catalog(Item("khz-coat-01", "  Yun palto ", size, color));

        var result = await CreateService(db).SyncAllAsync(company.CompanyId, CancellationToken.None);

        result.Value!.Created.Should().Be(1);
        await using var check = _database.CreateContext();
        var product = await check.Products.SingleAsync();
        product.Name.Should().Be(expected);
        product.Barcode.Should().Be("KHZ-COAT-01"); // the SKU, upper-cased
        product.Code.Should().Be("MHS-001");
    }

    [Fact]
    public async Task A_long_variant_name_is_cut_to_the_200_character_column()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        Catalog(Item("KHZ-LONG", new string('a', 195), "XL", "Tünd göy"));

        await CreateService(db).SyncAllAsync(company.CompanyId, CancellationToken.None);

        await using var check = _database.CreateContext();
        var name = (await check.Products.SingleAsync()).Name;
        name.Should().HaveLength(200).And.StartWith(new string('a', 195) + " (");
    }

    [Fact]
    public async Task Each_variant_becomes_its_own_product_and_a_renamed_one_is_updated()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        Catalog(Item("KHZ-1-M", "Palto", "M", "Qara"), Item("KHZ-1-L", "Palto", "L", "Qara"));
        await CreateService(db).SyncAllAsync(company.CompanyId, CancellationToken.None);

        Catalog(Item("KHZ-1-M", "Palto", "M", "Qəhvəyi"), Item("KHZ-1-L", "Palto", "L", "Qara"));
        var second = await CreateService(db).SyncAllAsync(company.CompanyId, CancellationToken.None);

        second.Value!.Updated.Should().Be(1);
        second.Value.Unchanged.Should().Be(1);
        await using var check = _database.CreateContext();
        (await check.Products.OrderBy(p => p.Barcode).Select(p => p.Name).ToListAsync())
            .Should().Equal("Palto (Qara / L)", "Palto (Qəhvəyi / M)");
    }

    [Fact]
    public async Task A_stock_that_cannot_be_lowered_is_skipped_never_forced_negative()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var product = await TestData.ProductAsync(db, company, "MHS-001", "Palto", "KHZ-1-M");
        await TestData.StockAsync(db, company, product.Id, 10m);
        _stock.Setup(s => s.TryDecreaseAsync(company.CompanyId, company.WarehouseId, product.Id, 7m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Catalog(Item("KHZ-1-M", "Palto", null, null, stock: 3, price: 10m));

        var result = await CreateService(db).SyncAllAsync(company.CompanyId, CancellationToken.None);

        result.Value!.Skipped.Should().Be(1);
        result.Value.StockAdjusted.Should().Be(0);
        result.Value.Errors.Should().ContainSingle().Which.Should().Be("KHZ-1-M: stok azaldıla bilmədi (qalıq dəyişib).");
        _stock.Verify(s => s.IncreaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Stock_follows_khazar_through_the_stock_writer()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        Catalog(Item("KHZ-NEW", "Şərf", null, "Bej", stock: 4));

        var result = await CreateService(db).SyncAllAsync(company.CompanyId, CancellationToken.None);

        result.Value!.StockAdjusted.Should().Be(1);
        _stock.Verify(s => s.IncreaseAsync(company.CompanyId, company.WarehouseId, It.IsAny<string>(), 4m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Invalid_items_are_reported_and_deactivated_products_are_left_alone()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        await TestData.ProductAsync(db, company, "MHS-001", "Köhnə", "KHZ-OLD", isActive: false);
        Catalog(Item("KHZ-OLD", "Köhnə", null, null), Item(" ", "SKU-suz", null, null), Item("KHZ-NEG", "Mənfi", null, null, price: -1m));

        var result = await CreateService(db).SyncAllAsync(company.CompanyId, CancellationToken.None);

        result.Value!.Created.Should().Be(0);
        result.Value.Errors.Should().BeEquivalentTo(
            "KHZ-OLD: NexusERP-də deaktiv məhsuldur.",
            "'SKU-suz': SKU boşdur və ya 50 simvoldan uzundur.",
            "KHZ-NEG: qiymət yararsızdır.");
    }

    [Fact]
    public async Task An_unreachable_khazar_is_reported_as_service_unavailable()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        _khazar.Setup(k => k.GetCatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new KhazarCatalogResult(null, "Khazar əlçatan deyil"));

        var result = await CreateService(db).SyncAllAsync(company.CompanyId, CancellationToken.None);

        result.ErrorType.Should().Be(ErrorType.ServiceUnavailable);
        result.Error.Should().Be("Khazar əlçatan deyil");
    }
}
