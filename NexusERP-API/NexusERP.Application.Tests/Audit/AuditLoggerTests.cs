using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Audit;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Application.Customers;
using NexusERP.Application.Tests.TestSupport;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Tests.Audit;

/// <summary>The real audit logger: what an entry records, and that a counterparty delete leaves one behind.</summary>
public sealed class AuditLoggerTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private static Mock<ICurrentUserContext> CurrentUser(string? userId, string? ip = "10.0.0.7")
    {
        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.SetupGet(c => c.IpAddress).Returns(ip);
        return currentUser;
    }

    [Fact]
    public async Task LogAsync_records_who_did_what_with_the_modules_department()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var user = new User { CompanyId = company.CompanyId, Name = "Əli Məmmədov", Email = "ali@test.az", Role = "itadmin" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var logger = new AuditLogger(db, CurrentUser(user.Id).Object, new ListLogger<AuditLogger>());
        await logger.LogAsync(company.CompanyId, AuditModule.Finance, AuditAction.Delete, "'KNT-001 · X' kontragenti silindi", CancellationToken.None);

        await using var check = _database.CreateContext();
        var entry = await check.AuditLogs.SingleAsync();
        entry.CompanyId.Should().Be(company.CompanyId);
        entry.UserId.Should().Be(user.Id);
        entry.UserName.Should().Be("Əli Məmmədov");
        entry.UserRole.Should().Be("itadmin");
        entry.Department.Should().Be("Maliyyə");
        entry.ModuleName.Should().Be(AuditModule.Finance);
        entry.ActionType.Should().Be(AuditAction.Delete);
        entry.Description.Should().Be("Əli Məmmədov tərəfindən 'KNT-001 · X' kontragenti silindi");
        entry.IpAddress.Should().Be("10.0.0.7");
    }

    [Theory]
    [InlineData(AuditModule.Finance, "Maliyyə")]
    [InlineData(AuditModule.HR, "İHR")]
    [InlineData(AuditModule.Inventory, "Anbar")]
    [InlineData(AuditModule.Retail, "Şəbəkə")]
    public void DepartmentOf_maps_every_module(AuditModule module, string department) =>
        AuditLogger.DepartmentOf(module).Should().Be(department);

    [Fact]
    public async Task LogAsync_without_a_signed_in_user_is_attributed_to_the_system()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);

        var logger = new AuditLogger(db, CurrentUser(null, ip: null).Object, new ListLogger<AuditLogger>());
        await logger.LogAsync(company.CompanyId, AuditModule.Inventory, AuditAction.Update, "stok sıfırlandı", CancellationToken.None);

        await using var check = _database.CreateContext();
        var entry = await check.AuditLogs.SingleAsync();
        entry.UserName.Should().Be("Sistem");
        entry.UserRole.Should().Be("system");
        entry.Description.Should().Be("Sistem tərəfindən stok sıfırlandı");
    }

    [Fact]
    public async Task A_counterparty_delete_writes_an_audit_row()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var customer = await TestData.CustomerAsync(db, company.CompanyId, "KNT-007", "Sumqayıt Ticarət");
        var audit = new AuditLogger(db, CurrentUser(null).Object, new ListLogger<AuditLogger>());

        await new CustomerService(db, audit, new ListLogger<CustomerService>())
            .DeleteAsync(company.CompanyId, customer.Id, CancellationToken.None);

        await using var check = _database.CreateContext();
        var entry = await check.AuditLogs.SingleAsync();
        entry.ActionType.Should().Be(AuditAction.Delete);
        entry.ModuleName.Should().Be(AuditModule.Finance);
        entry.Description.Should().Be("Sistem tərəfindən 'KNT-007 · Sumqayıt Ticarət' kontragenti silindi");
    }
}
