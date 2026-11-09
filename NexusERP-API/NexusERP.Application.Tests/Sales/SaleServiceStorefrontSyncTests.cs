using NexusERP.Application.Audit;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Application.Integration;
using NexusERP.Application.Sales;
using NexusERP.Application.Tests.TestSupport;
using NexusERP.Domain.Entities;
using NexusERP.Infrastructure.Persistence;

namespace NexusERP.Application.Tests.Sales;

/// <summary>
/// A store-side sale moves stock, so the Khazar storefront must be told once the sale is committed. The stock
/// writer is mocked (its SQL is SQL Server's concern); what is tested is when the storefront sync is asked.
/// </summary>
public sealed class SaleServiceStorefrontSyncTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly Mock<IStockWriter> _stock = new();
    private readonly Mock<IStorefrontStockSync> _storefront = new();

    public SaleServiceStorefrontSyncTests() =>
        _stock.Setup(w => w.TryDecreaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    public void Dispose() => _database.Dispose();

    private SaleService CreateService(AppDbContext db) =>
        new(db, _stock.Object, Mock.Of<IAuditLogger>(), Mock.Of<IKhazarOrderClient>(), _storefront.Object);

    private static async Task<(SeededCompany Company, Store Store, Product Product)> SeedAsync(AppDbContext db)
    {
        var company = await TestData.CompanyAsync(db);
        var store = new Store { CompanyId = company.CompanyId, WarehouseId = company.WarehouseId, Code = "MGZ-001", Name = "Mağaza" };
        db.Stores.Add(store);
        await db.SaveChangesAsync();

        var product = await TestData.ProductAsync(db, company, "MHS-001", "Palto M", "2000000000011");
        return (company, store, product);
    }

    private static CreateSaleRequestDto Request(Store store, Product product) =>
        new(store.Id, null, null, null, [new SaleItemRequestDto(product.Id, 1m, null)]);

    [Fact]
    public async Task PosCheckoutAsync_pushes_the_sold_products_to_the_storefront_after_committing()
    {
        await using var db = _database.CreateContext();
        var (company, store, product) = await SeedAsync(db);

        var result = await CreateService(db).PosCheckoutAsync(company.CompanyId, Request(store, product), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _storefront.Verify(
            s => s.SyncAsync(company.CompanyId, It.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { product.Id })), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PosCheckoutAsync_does_not_touch_the_storefront_when_stock_is_short()
    {
        await using var db = _database.CreateContext();
        var (company, store, product) = await SeedAsync(db);
        _stock.Setup(w => w.TryDecreaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateService(db).PosCheckoutAsync(company.CompanyId, Request(store, product), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _storefront.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Approving_a_draft_pushes_the_sold_products_to_the_storefront()
    {
        await using var db = _database.CreateContext();
        var (company, store, product) = await SeedAsync(db);
        var service = CreateService(db);

        var draft = await service.CreateAsync(company.CompanyId, Request(store, product), CancellationToken.None);
        _storefront.VerifyNoOtherCalls();

        var approved = await service.ApproveAsync(company.CompanyId, draft.Value!.Id, CancellationToken.None);

        approved.IsSuccess.Should().BeTrue();
        _storefront.Verify(
            s => s.SyncAsync(company.CompanyId, It.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { product.Id })), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Cancelling_an_approved_sale_pushes_the_returned_stock_to_the_storefront()
    {
        await using var db = _database.CreateContext();
        var (company, store, product) = await SeedAsync(db);
        var service = CreateService(db);
        var sale = await service.PosCheckoutAsync(company.CompanyId, Request(store, product), CancellationToken.None);
        _storefront.Invocations.Clear();

        var cancelled = await service.CancelAsync(company.CompanyId, sale.Value!.Id, CancellationToken.None);

        cancelled.IsSuccess.Should().BeTrue();
        _storefront.Verify(
            s => s.SyncAsync(company.CompanyId, It.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { product.Id })), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
