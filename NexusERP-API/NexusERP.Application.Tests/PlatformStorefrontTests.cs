using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexusERP.Application.Admin;
using NexusERP.Application.Auth;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Application.Dashboard;
using NexusERP.Application.Tests.TestSupport;

namespace NexusERP.Application.Tests;

/// <summary>The platform admin connects a company to a storefront by name, or disconnects it.</summary>
public sealed class PlatformStorefrontTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private static PlatformAdminService Create(Infrastructure.Persistence.AppDbContext db) =>
        new(db, new Mock<IDashboardService>().Object, new Mock<IJwtTokenGenerator>().Object,
            new Mock<IPasswordHasher>().Object, NullLogger<PlatformAdminService>.Instance);

    [Fact]
    public async Task A_name_connects_and_a_blank_name_disconnects()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db, "Başqa MMC", storefrontName: null);
        var service = Create(db);

        (await service.UpdateStorefrontAsync(company.CompanyId, new UpdateTenantStorefrontRequestDto("  Khazar "), CancellationToken.None))
            .IsSuccess.Should().BeTrue();
        await using (var check = _database.CreateContext())
            (await check.Companies.SingleAsync(c => c.Id == company.CompanyId)).StorefrontName.Should().Be("Khazar");

        (await service.UpdateStorefrontAsync(company.CompanyId, new UpdateTenantStorefrontRequestDto(" "), CancellationToken.None))
            .IsSuccess.Should().BeTrue();
        await using (var check = _database.CreateContext())
            (await check.Companies.SingleAsync(c => c.Id == company.CompanyId)).StorefrontName.Should().BeNull();
    }

    [Fact]
    public async Task An_unknown_company_is_404_and_a_long_name_is_rejected()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db, "Başqa MMC", storefrontName: null);
        var service = Create(db);

        (await service.UpdateStorefrontAsync("nope", new UpdateTenantStorefrontRequestDto("Khazar"), CancellationToken.None))
            .ErrorType.Should().Be(ErrorType.NotFound);
        (await service.UpdateStorefrontAsync(company.CompanyId, new UpdateTenantStorefrontRequestDto(new string('x', 101)), CancellationToken.None))
            .ErrorType.Should().Be(ErrorType.Validation);
    }
}
