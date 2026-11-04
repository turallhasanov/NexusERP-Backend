using Microsoft.Extensions.Logging.Abstractions;
using NexusERP.Application.Integration;
using NexusERP.Application.Tests.TestSupport;

namespace NexusERP.Application.Tests.Integration;

/// <summary>Stock sent to Khazar after a stock change: floored, never negative, only for shown products, never throws.</summary>
public sealed class StorefrontStockSyncTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly Mock<IKhazarProductClient> _khazar = new();
    private readonly List<KhazarProductPush> _pushes = [];

    public StorefrontStockSyncTests() =>
        _khazar.Setup(k => k.PushProductAsync(It.IsAny<KhazarProductPush>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<KhazarProductPush, Stream?, string?, CancellationToken>((p, _, _, _) => _pushes.Add(p))
            .ReturnsAsync(KhazarProductResult.Ok);

    public void Dispose() => _database.Dispose();

    [Theory]
    [InlineData(7.9, 7)]
    [InlineData(0.4, 0)]
    [InlineData(-3, 0)]
    public async Task Sends_the_floored_non_negative_total(double quantity, int expected)
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var product = await TestData.ProductAsync(db, company, "MHS-001", "Palto", "2000000000011", showOnWebsite: true);
        await TestData.StockAsync(db, company, product.Id, (decimal)quantity);

        await new StorefrontStockSync(db, _khazar.Object, NullLogger<StorefrontStockSync>.Instance)
            .SyncAsync(company.CompanyId, [product.Id], CancellationToken.None);

        var push = _pushes.Should().ContainSingle().Subject;
        push.Sku.Should().Be("2000000000011");
        push.Stock.Should().Be(expected);
        push.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task Skips_products_that_are_not_shown_on_the_website()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var hidden = await TestData.ProductAsync(db, company, "MHS-001", "Palto", "2000000000011");

        await new StorefrontStockSync(db, _khazar.Object, NullLogger<StorefrontStockSync>.Instance)
            .SyncAsync(company.CompanyId, [hidden.Id], CancellationToken.None);

        _pushes.Should().BeEmpty();
    }

    [Fact]
    public async Task Swallows_khazar_failures_and_exceptions()
    {
        await using var db = _database.CreateContext();
        var company = await TestData.CompanyAsync(db);
        var a = await TestData.ProductAsync(db, company, "MHS-001", "Palto", "2000000000011", showOnWebsite: true);
        var b = await TestData.ProductAsync(db, company, "MHS-002", "Şərf", "2000000000028", showOnWebsite: true);
        _khazar.Reset();
        _khazar.SetupSequence(k => k.PushProductAsync(It.IsAny<KhazarProductPush>(), null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"))
            .ReturnsAsync(KhazarProductResult.Failed("xəta"));

        var act = () => new StorefrontStockSync(db, _khazar.Object, NullLogger<StorefrontStockSync>.Instance)
            .SyncAsync(company.CompanyId, [a.Id, b.Id], CancellationToken.None);

        await act.Should().NotThrowAsync();
        _khazar.Verify(k => k.PushProductAsync(It.IsAny<KhazarProductPush>(), null, null, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
