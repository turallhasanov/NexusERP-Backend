using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexusERP.Application.Audit;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Application.Customers;
using NexusERP.Application.Purchases;
using NexusERP.Application.Tests.TestSupport;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Tests.Customers;

/// <summary>Counterparty soft delete: removed when unused, deactivated when documents use it, never for system accounts.</summary>
public sealed class CustomerServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly Mock<IAuditLogger> _audit = new();
    private readonly ListLogger<CustomerService> _logger = new();

    public void Dispose() => _database.Dispose();

    private CustomerService CreateService(IApplicationDbContext db) => new(db, _audit.Object, _logger);

    [Fact]
    public async Task DeleteAsync_removes_a_counterparty_no_document_uses_and_writes_an_audit_entry()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var customer = await TestData.CustomerAsync(db, company.CompanyId, "KNT-001", "Bakı Tekstil");

        var result = await CreateService(db).DeleteAsync(company.CompanyId, customer.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Deactivated.Should().BeFalse();
        result.Value.Message.Should().Be("Kontragent uğurla silindi.");

        await using var check = _database.CreateContext();
        (await check.Customers.AnyAsync(c => c.Id == customer.Id)).Should().BeFalse();

        _audit.Verify(a => a.LogAsync(company.CompanyId, AuditModule.Finance, AuditAction.Delete,
            "'KNT-001 · Bakı Tekstil' kontragenti silindi", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_deactivates_a_counterparty_a_purchase_uses_instead_of_removing_it()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var customer = await TestData.CustomerAsync(db, company.CompanyId, "KNT-001", "Gəncə Parça");
        var product = await TestData.ProductAsync(db, company, "MHS-001", "Köynək", "2000000000011");
        await TestData.PurchaseAsync(db, company, customer.Id, PurchaseStatus.Approved, "ALS-001", (product.Id, 2m, 15m));

        var result = await CreateService(db).DeleteAsync(company.CompanyId, customer.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Deactivated.Should().BeTrue();
        result.Value.Message.Should().Contain("deaktiv edildi");

        // Soft delete: the row stays for the purchase that references it, flagged inactive.
        await using var check = _database.CreateContext();
        var stored = await check.Customers.SingleAsync(c => c.Id == customer.Id);
        stored.IsActive.Should().BeFalse();
        stored.Name.Should().Be("Gəncə Parça");

        _audit.Verify(a => a.LogAsync(company.CompanyId, AuditModule.Finance, AuditAction.Delete,
            "'KNT-001 · Gəncə Parça' kontragenti silindi (sənədlərdə istifadə olunduğu üçün deaktiv edildi)",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_refuses_a_system_counterparty_and_changes_nothing()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var online = await TestData.CustomerAsync(db, company.CompanyId, "KNT-001", "Khazar Online Müştəri", systemKey: "khazar-online");

        var result = await CreateService(db).DeleteAsync(company.CompanyId, online.Id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
        result.Error.Should().Be("Sistem kontragenti silinə bilməz.");

        await using var check = _database.CreateContext();
        (await check.Customers.SingleAsync(c => c.Id == online.Id)).IsActive.Should().BeTrue();
        _audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteAsync_treats_an_already_deactivated_counterparty_as_missing_and_logs_why()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var customer = await TestData.CustomerAsync(db, company.CompanyId, "KNT-001", "Köhnə Təchizatçı", isActive: false);

        var result = await CreateService(db).DeleteAsync(company.CompanyId, customer.Id, CancellationToken.None);

        result.ErrorType.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be("Kontragent tapılmadı.");
        _audit.VerifyNoOtherCalls();

        // The diagnostic warning names the searched id and says the row exists but is inactive.
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning)
            .Which.Message.Should().Contain(customer.Id).And.Contain("Row exists: True").And.Contain("active: False");
    }

    [Fact]
    public async Task DeleteAsync_does_not_touch_another_companys_counterparty()
    {
        await using var db = _database.CreateContext();
        var mine = await TestData.CompanyAsync(db, "Mənim şirkətim");
        var other = await TestData.CompanyAsync(db, "Başqa şirkət");
        var foreign = await TestData.CustomerAsync(db, other.CompanyId, "KNT-001", "Yad Kontragent");

        var result = await CreateService(db).DeleteAsync(mine.CompanyId, foreign.Id, CancellationToken.None);

        result.ErrorType.Should().Be(ErrorType.NotFound);
        _logger.Entries.Should().ContainSingle().Which.Message.Should().Contain($"row company: {other.CompanyId}");

        await using var check = _database.CreateContext();
        (await check.Customers.SingleAsync(c => c.Id == foreign.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_for_an_unknown_id_is_not_found()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);

        var result = await CreateService(db).DeleteAsync(company.CompanyId, Guid.Empty.ToString(), CancellationToken.None);

        result.ErrorType.Should().Be(ErrorType.NotFound);
        result.Error.Should().Be("Kontragent tapılmadı.");
        _logger.Entries.Should().ContainSingle().Which.Message.Should().Contain("Row exists: False");
    }

    [Fact]
    public async Task GetAllAsync_flags_a_deleted_counterparty_inactive_and_drops_a_removed_one()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var product = await TestData.ProductAsync(db, company, "MHS-001", "Şalvar", "2000000000028");
        var kept = await TestData.CustomerAsync(db, company.CompanyId, "KNT-001", "Aktiv Kontragent");
        var used = await TestData.CustomerAsync(db, company.CompanyId, "KNT-002", "İstifadə olunan");
        var unused = await TestData.CustomerAsync(db, company.CompanyId, "KNT-003", "İstifadə olunmayan");
        await TestData.PurchaseAsync(db, company, used.Id, PurchaseStatus.Draft, "ALS-001", (product.Id, 1m, 5m));

        var service = CreateService(db);
        await service.DeleteAsync(company.CompanyId, used.Id, CancellationToken.None);
        await service.DeleteAsync(company.CompanyId, unused.Id, CancellationToken.None);

        var list = await service.GetAllAsync(company.CompanyId, CancellationToken.None);

        // The list still carries the deactivated row (documents show its name) with isActive = false, which is what
        // the pages filter on; the removed one is gone.
        list.Value!.Items.Select(c => (c.Id, c.IsActive)).Should().BeEquivalentTo([(kept.Id, true), (used.Id, false)]);
    }

    [Fact]
    public async Task A_deleted_counterparty_cannot_be_picked_for_a_new_purchase()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var product = await TestData.ProductAsync(db, company, "MHS-001", "Palto", "2000000000035");
        var customer = await TestData.CustomerAsync(db, company.CompanyId, "KNT-001", "Təchizatçı");
        await TestData.PurchaseAsync(db, company, customer.Id, PurchaseStatus.Draft, "ALS-001", (product.Id, 1m, 5m));
        await CreateService(db).DeleteAsync(company.CompanyId, customer.Id, CancellationToken.None);

        var purchases = new PurchaseService(db, Mock.Of<IStockWriter>(), _audit.Object);
        var result = await purchases.CreateAsync(company.CompanyId,
            new CreatePurchaseRequestDto(company.WarehouseId, customer.Id, null, [new PurchaseItemRequestDto(product.Id, 1m, 5m)]),
            CancellationToken.None);

        result.ErrorType.Should().Be(ErrorType.Validation);
        result.Error.Should().Be("Seçilmiş kontragent aktiv deyil.");
    }

    [Fact]
    public async Task Deleting_twice_deactivates_once_then_reports_not_found()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var product = await TestData.ProductAsync(db, company, "MHS-001", "Gödəkçə", "2000000000042");
        var customer = await TestData.CustomerAsync(db, company.CompanyId, "KNT-001", "Təkrar");
        await TestData.PurchaseAsync(db, company, customer.Id, PurchaseStatus.Draft, "ALS-001", (product.Id, 1m, 5m));
        var service = CreateService(db);

        (await service.DeleteAsync(company.CompanyId, customer.Id, CancellationToken.None)).Value!.Deactivated.Should().BeTrue();
        (await service.DeleteAsync(company.CompanyId, customer.Id, CancellationToken.None)).ErrorType.Should().Be(ErrorType.NotFound);

        _audit.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<AuditModule>(), It.IsAny<AuditAction>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
