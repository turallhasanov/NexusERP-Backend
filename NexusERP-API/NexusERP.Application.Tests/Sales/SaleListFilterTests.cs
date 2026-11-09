using NexusERP.Application.Audit;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Application.Integration;
using NexusERP.Application.Sales;
using NexusERP.Application.Tests.TestSupport;
using NexusERP.Domain.Entities;
using NexusERP.Infrastructure.Persistence;

namespace NexusERP.Application.Tests.Sales;

/// <summary>GET /api/sales filters: by counterparty id, and by text over customer name, code, phone and document number.</summary>
public sealed class SaleListFilterTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private static SaleService CreateService(AppDbContext db) =>
        new(db, Mock.Of<IStockWriter>(), Mock.Of<IAuditLogger>(), Mock.Of<IKhazarOrderClient>(), Mock.Of<IStorefrontStockSync>());

    private sealed record Seeded(string CompanyId, string TuralId, string AliId);

    // Three sales: one to each of two counterparties, and one walk-in retail sale with a stored shopper.
    private static async Task<Seeded> SeedAsync(AppDbContext db)
    {
        var company = await TestData.CompanyAsync(db);
        var store = new Store { CompanyId = company.CompanyId, WarehouseId = company.WarehouseId, Code = "MGZ-001", Name = "Mağaza" };
        db.Stores.Add(store);
        await db.SaveChangesAsync();

        var tural = await TestData.CustomerAsync(db, company.CompanyId, "KNT-001", "Tural Hasanov");
        tural.Phone = "+994501234567";
        var ali = await TestData.CustomerAsync(db, company.CompanyId, "KNT-002", "Əli Əliyev");
        ali.Phone = "+994559998877";
        await db.SaveChangesAsync();

        var product = await TestData.ProductAsync(db, company, "MHS-001", "Palto", "2000000000011");
        var service = CreateService(db);

        foreach (var customerId in new[] { tural.Id, ali.Id, null })
        {
            var sale = await service.CreateAsync(
                company.CompanyId,
                new CreateSaleRequestDto(store.Id, null, customerId, null, [new SaleItemRequestDto(product.Id, 1m, null)]),
                CancellationToken.None);
            sale.IsSuccess.Should().BeTrue();
        }

        return new Seeded(company.CompanyId, tural.Id, ali.Id);
    }

    private static async Task<string[]> NamesAsync(AppDbContext db, Seeded s, string? customerId = null, string? search = null)
    {
        var result = await CreateService(db).GetAllAsync(s.CompanyId, customerId, search, CancellationToken.None);
        return result.Value!.Items.Select(i => i.Customer?.Name ?? "—").ToArray();
    }

    [Fact]
    public async Task Without_filters_every_sale_is_listed()
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);

        (await NamesAsync(db, s)).Should().HaveCount(3);
    }

    [Fact]
    public async Task CustomerId_keeps_only_that_counterpartys_sales()
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);

        (await NamesAsync(db, s, customerId: s.TuralId)).Should().Equal("Tural Hasanov");
    }

    [Theory]
    [InlineData("Tural")]
    [InlineData("Hasanov")]
    [InlineData("KNT-001")]
    [InlineData("0501234567")]
    [InlineData("50123")]
    [InlineData("050 123 45 67")]
    [InlineData("+994 (50) 123-45-67")]
    public async Task Search_finds_a_counterparty_by_name_code_or_phone(string text)
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);

        (await NamesAsync(db, s, search: text)).Should().Equal("Tural Hasanov");
    }

    [Fact]
    public async Task Search_with_no_match_is_empty_and_blank_search_lists_everything()
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);

        (await NamesAsync(db, s, search: "nobody")).Should().BeEmpty();
        (await NamesAsync(db, s, search: "   ")).Should().HaveCount(3);
    }

    [Fact]
    public async Task Search_by_document_number_finds_one_sale()
    {
        await using var db = _database.CreateContext();
        var s = await SeedAsync(db);
        var first = (await CreateService(db).GetAllAsync(s.CompanyId, null, null, CancellationToken.None)).Value!.Items.Last();

        (await CreateService(db).GetAllAsync(s.CompanyId, null, first.DocumentNumber, CancellationToken.None))
            .Value!.Items.Should().ContainSingle(i => i.Id == first.Id);
    }
}
