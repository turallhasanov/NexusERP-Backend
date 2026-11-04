using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Audit;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Application.Integration;
using NexusERP.Application.Purchases;
using NexusERP.Application.Tests.TestSupport;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;
using NexusERP.Infrastructure.Persistence;

namespace NexusERP.Application.Tests.Purchases;

/// <summary>
/// Approving adds stock; cancelling takes it back without ever driving a balance negative. The stock writer is mocked:
/// its own atomic SQL is SQL Server's concern, what is tested here is what the service asks of it and when.
/// </summary>
public sealed class PurchaseServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly Mock<IStockWriter> _stock = new(MockBehavior.Strict);
    private readonly Mock<IAuditLogger> _audit = new();
    private readonly Mock<IStorefrontStockSync> _storefront = new();

    public void Dispose() => _database.Dispose();

    private PurchaseService CreateService(AppDbContext db) => new(db, _stock.Object, _audit.Object, _storefront.Object);

    private sealed record Seeded(SeededCompany Company, Customer Supplier, Product Coat, Product Scarf);

    private static async Task<Seeded> SeedAsync(AppDbContext db)
    {
        var company = await TestData.CompanyAsync(db);
        var supplier = await TestData.CustomerAsync(db, company.CompanyId, "KNT-001", "Təchizatçı");
        var coat = await TestData.ProductAsync(db, company, "MHS-001", "Palto", "2000000000011");
        var scarf = await TestData.ProductAsync(db, company, "MHS-002", "Şərf", "2000000000028");
        return new Seeded(company, supplier, coat, scarf);
    }

    private async Task<PurchaseStatus> StatusOfAsync(string purchaseId)
    {
        await using var check = _database.CreateContext();
        return await check.Purchases.Where(p => p.Id == purchaseId).Select(p => p.Status).SingleAsync();
    }

    private void SetupDecrease(Seeded s, string productId, decimal quantity, bool succeeds) =>
        _stock.Setup(w => w.TryDecreaseAsync(s.Company.CompanyId, s.Company.WarehouseId, productId, quantity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(succeeds);

    // ---- Cancel: the negative-stock guard ----

    [Fact]
    public async Task CancelAsync_refuses_when_the_goods_are_no_longer_in_stock_and_rolls_the_status_back()
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);
        var purchase = await TestData.PurchaseAsync(db, s.Company, s.Supplier.Id, PurchaseStatus.Approved, "ALS-001", (s.Coat.Id, 5m, 20m));
        await TestData.StockAsync(db, s.Company, s.Coat.Id, 3m);
        SetupDecrease(s, s.Coat.Id, 5m, succeeds: false);

        var result = await CreateService(db).CancelAsync(s.Company.CompanyId, purchase.Id, force: false, CancellationToken.None);

        result.ErrorType.Should().Be(ErrorType.StockShortage);
        result.Error.Should().Be(
            "'ALS-001' alışını ləğv etmək mümkün deyil: Palto malının bir hissəsi artıq anbarda yoxdur (Qalıq: 3, Geri götürülməli: 5).");

        // The status was set to Cancelled inside the transaction; returning without Commit must undo it.
        (await StatusOfAsync(purchase.Id)).Should().Be(PurchaseStatus.Approved);
        _stock.Verify(w => w.TryDecreaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Once);
        _audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Forced_CancelAsync_takes_only_what_is_left_so_the_balance_ends_at_zero()
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);
        var purchase = await TestData.PurchaseAsync(db, s.Company, s.Supplier.Id, PurchaseStatus.Approved, "ALS-001", (s.Coat.Id, 5m, 20m));
        await TestData.StockAsync(db, s.Company, s.Coat.Id, 3m);
        SetupDecrease(s, s.Coat.Id, 5m, succeeds: false);
        SetupDecrease(s, s.Coat.Id, 3m, succeeds: true);

        var result = await CreateService(db).CancelAsync(s.Company.CompanyId, purchase.Id, force: true, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PurchaseStatus.Cancelled);
        (await StatusOfAsync(purchase.Id)).Should().Be(PurchaseStatus.Cancelled);

        // Exactly the 5 asked for (refused), then the 3 on hand: nothing more is ever taken out.
        _stock.Verify(w => w.TryDecreaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _audit.Verify(a => a.LogAsync(s.Company.CompanyId, AuditModule.Finance, AuditAction.Update,
            It.Is<string>(m => m.Contains("məcburi ləğv edildi") && m.Contains("Palto: 2 ədəd çatmırdı")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Forced_CancelAsync_with_an_empty_warehouse_takes_nothing_out()
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);
        var purchase = await TestData.PurchaseAsync(db, s.Company, s.Supplier.Id, PurchaseStatus.Approved, "ALS-001", (s.Coat.Id, 2m, 20m));
        SetupDecrease(s, s.Coat.Id, 2m, succeeds: false);

        var result = await CreateService(db).CancelAsync(s.Company.CompanyId, purchase.Id, force: true, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _stock.Verify(w => w.TryDecreaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_of_an_approved_purchase_takes_back_each_products_total_once()
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);
        var purchase = await TestData.PurchaseAsync(db, s.Company, s.Supplier.Id, PurchaseStatus.Approved, "ALS-001",
            (s.Coat.Id, 2m, 20m), (s.Scarf.Id, 1m, 5m), (s.Coat.Id, 3m, 22m));
        SetupDecrease(s, s.Coat.Id, 5m, succeeds: true);
        SetupDecrease(s, s.Scarf.Id, 1m, succeeds: true);

        var result = await CreateService(db).CancelAsync(s.Company.CompanyId, purchase.Id, force: false, CancellationToken.None);

        result.Value!.Status.Should().Be(PurchaseStatus.Cancelled);
        _stock.VerifyAll();
        _audit.Verify(a => a.LogAsync(s.Company.CompanyId, AuditModule.Finance, AuditAction.Update,
            "'ALS-001' alış qaiməsi ləğv edildi, məhsullar anbardan geri götürüldü (111.00 ₼)", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_of_a_draft_never_touches_stock()
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);
        var purchase = await TestData.PurchaseAsync(db, s.Company, s.Supplier.Id, PurchaseStatus.Draft, "ALS-001", (s.Coat.Id, 2m, 20m));

        var result = await CreateService(db).CancelAsync(s.Company.CompanyId, purchase.Id, force: false, CancellationToken.None);

        result.Value!.Status.Should().Be(PurchaseStatus.Cancelled);
        _stock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CancelAsync_repeated_after_a_lost_response_does_not_take_stock_out_twice()
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);
        var purchase = await TestData.PurchaseAsync(db, s.Company, s.Supplier.Id, PurchaseStatus.Cancelled, "ALS-001", (s.Coat.Id, 2m, 20m));

        var result = await CreateService(db).CancelAsync(s.Company.CompanyId, purchase.Id, force: false, CancellationToken.None);

        result.Value!.Status.Should().Be(PurchaseStatus.Cancelled);
        _stock.VerifyNoOtherCalls();
        _audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CancelAsync_is_refused_while_a_cash_payment_is_linked()
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);
        var purchase = await TestData.PurchaseAsync(db, s.Company, s.Supplier.Id, PurchaseStatus.Approved, "ALS-001", (s.Coat.Id, 2m, 20m));
        var register = new CashRegister { CompanyId = s.Company.CompanyId, Code = "KAS-001", Name = "Əsas kassa" };
        db.CashRegisters.Add(register);
        db.Transactions.Add(new Transaction
        {
            CompanyId = s.Company.CompanyId, CashRegisterId = register.Id, PurchaseId = purchase.Id,
            TransactionType = TransactionType.Expense, Amount = 40m, Description = "Ödəniş",
            TransactionDate = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).CancelAsync(s.Company.CompanyId, purchase.Id, force: true, CancellationToken.None);

        result.ErrorType.Should().Be(ErrorType.Conflict);
        result.Error.Should().Be("'ALS-001' alışına kassa ödənişi bağlıdır, ləğv etmək mümkün deyil.");
        _stock.VerifyNoOtherCalls();
    }

    // ---- Approve ----

    [Fact]
    public async Task ApproveAsync_adds_each_products_total_and_takes_the_last_lines_price()
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);
        var purchase = await TestData.PurchaseAsync(db, s.Company, s.Supplier.Id, PurchaseStatus.Draft, "ALS-001",
            (s.Coat.Id, 2m, 20m), (s.Scarf.Id, 1m, 5m), (s.Coat.Id, 3m, 22m));
        _stock.Setup(w => w.IncreaseAsync(s.Company.CompanyId, s.Company.WarehouseId, s.Coat.Id, 5m, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _stock.Setup(w => w.IncreaseAsync(s.Company.CompanyId, s.Company.WarehouseId, s.Scarf.Id, 1m, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateService(db).ApproveAsync(s.Company.CompanyId, purchase.Id, CancellationToken.None);

        result.Value!.Status.Should().Be(PurchaseStatus.Approved);
        _stock.VerifyAll();

        await using var check = _database.CreateContext();
        (await check.Products.SingleAsync(p => p.Id == s.Coat.Id)).PurchasePrice.Should().Be(22m);
        (await check.Products.SingleAsync(p => p.Id == s.Scarf.Id)).PurchasePrice.Should().Be(5m);
    }

    [Fact]
    public async Task ApproveAsync_sends_the_approved_products_to_the_storefront_after_commit()
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);
        var purchase = await TestData.PurchaseAsync(db, s.Company, s.Supplier.Id, PurchaseStatus.Draft, "ALS-001", (s.Coat.Id, 2m, 20m));
        _stock.Setup(w => w.IncreaseAsync(s.Company.CompanyId, s.Company.WarehouseId, s.Coat.Id, 2m, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateService(db).ApproveAsync(s.Company.CompanyId, purchase.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _storefront.Verify(f => f.SyncAsync(s.Company.CompanyId,
            It.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { s.Coat.Id })), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_twice_adds_stock_only_once()
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);
        var purchase = await TestData.PurchaseAsync(db, s.Company, s.Supplier.Id, PurchaseStatus.Draft, "ALS-001", (s.Coat.Id, 2m, 20m));
        _stock.Setup(w => w.IncreaseAsync(s.Company.CompanyId, s.Company.WarehouseId, s.Coat.Id, 2m, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = CreateService(db);

        await service.ApproveAsync(s.Company.CompanyId, purchase.Id, CancellationToken.None);
        var second = await service.ApproveAsync(s.Company.CompanyId, purchase.Id, CancellationToken.None);

        second.ErrorType.Should().Be(ErrorType.Conflict);
        second.Error.Should().Be("Sənəd artıq təsdiqlənib.");
        _stock.Verify(w => w.IncreaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Create: line validation ----

    [Theory]
    [InlineData(0, 5, "Sətir 1: miqdar sıfırdan böyük olmalıdır.")]
    [InlineData(-2, 5, "Sətir 1: miqdar sıfırdan böyük olmalıdır.")]
    [InlineData(0.0004, 5, "Sətir 1: miqdar sıfırdan böyük olmalıdır.")] // rounds to 0.000 at the column's scale
    [InlineData(1, -1, "Sətir 1: qiymət mənfi ola bilməz.")]
    public async Task CreateAsync_validates_every_line(double quantity, double unitPrice, string error)
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);

        var result = await CreateService(db).CreateAsync(s.Company.CompanyId,
            new CreatePurchaseRequestDto(s.Company.WarehouseId, s.Supplier.Id, null,
                [new PurchaseItemRequestDto(s.Coat.Id, (decimal)quantity, (decimal)unitPrice)]),
            CancellationToken.None);

        result.ErrorType.Should().Be(ErrorType.Validation);
        result.Error.Should().Be(error);
    }

    [Fact]
    public async Task CreateAsync_refuses_a_deactivated_product()
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);
        var retired = await TestData.ProductAsync(db, s.Company, "MHS-003", "Köhnə model", "2000000000035", isActive: false);

        var result = await CreateService(db).CreateAsync(s.Company.CompanyId,
            new CreatePurchaseRequestDto(s.Company.WarehouseId, s.Supplier.Id, null,
                [new PurchaseItemRequestDto(s.Coat.Id, 1m, 5m), new PurchaseItemRequestDto(retired.Id, 1m, 5m)]),
            CancellationToken.None);

        result.Error.Should().Be("Sətir 2: məhsul aktiv deyil.");
    }

    [Fact]
    public async Task CreateAsync_saves_a_numbered_draft_with_line_totals()
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);

        var result = await CreateService(db).CreateAsync(s.Company.CompanyId,
            new CreatePurchaseRequestDto(s.Company.WarehouseId, s.Supplier.Id, null,
                [new PurchaseItemRequestDto(s.Coat.Id, 1.5m, 19.995m), new PurchaseItemRequestDto(s.Scarf.Id, 2m, 4m)]),
            CancellationToken.None);

        result.Value!.DocumentNumber.Should().Be("ALS-001");
        result.Value.Status.Should().Be(PurchaseStatus.Draft);
        result.Value.TotalAmount.Should().Be(38.00m); // 1.5 × 20.00 + 2 × 4
        _stock.VerifyNoOtherCalls();
    }
}
