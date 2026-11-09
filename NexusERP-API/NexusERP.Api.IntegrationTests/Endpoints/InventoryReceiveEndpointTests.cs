using System.Net;
using Microsoft.EntityFrameworkCore;
using NexusERP.Api.IntegrationTests.TestSupport;
using NexusERP.Application.Integration;

namespace NexusERP.Api.IntegrationTests.Endpoints;

/// <summary>POST /api/inventory/receive: a manual stock receipt reaches the Khazar store.</summary>
public sealed class InventoryReceiveEndpointTests(NexusErpApiFactory factory) : IClassFixture<NexusErpApiFactory>
{
    private TestTenant Tenant => factory.Tenant;

    private async Task<string> CreateProductAsync(string barcode, bool showOnWebsite)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(Tenant.CategoryId), "categoryId" },
            { new StringContent(Tenant.UnitId), "unitId" },
            { new StringContent("Usaq Hoodie (S)"), "name" },
            { new StringContent(barcode), "barcode" },
            { new StringContent("59.90"), "salePrice" },
            { new StringContent(showOnWebsite ? "true" : "false"), "showOnWebsite" },
        };
        var response = await factory.CreateClient(Tenant.StaffToken).PostAsync("/api/products", form);
        return (await response.JsonAsync())["id"]!.GetValue<string>();
    }

    [Fact]
    public async Task Receiving_stock_adds_it_and_sends_the_floored_total_to_khazar()
    {
        var barcode = "RCV-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var productId = await CreateProductAsync(barcode, showOnWebsite: true);

        var response = await factory.CreateClient(Tenant.StaffToken).PostJsonAsync("/api/inventory/receive",
            new { warehouseId = Tenant.WarehouseId, productId, quantity = 7.9m });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.JsonAsync();
        body["ok"]!.GetValue<bool>().Should().BeTrue();
        body["totalQuantity"]!.GetValue<decimal>().Should().Be(7.9m);
        (await factory.WithDbAsync(db => db.Stocks.Where(s => s.ProductId == productId).SumAsync(s => (double)s.Quantity)))
            .Should().BeApproximately(7.9, 0.0001);
        factory.KhazarProducts.Verify(k => k.PushProductAsync(
            It.Is<KhazarProductPush>(p => p.Sku == barcode && p.Stock == 7 && p.IsPublished),
            It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_product_not_shown_on_the_website_is_not_sent()
    {
        var barcode = "RCV-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var productId = await CreateProductAsync(barcode, showOnWebsite: false);

        var response = await factory.CreateClient(Tenant.StaffToken).PostJsonAsync("/api/inventory/receive",
            new { warehouseId = Tenant.WarehouseId, productId, quantity = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.KhazarProducts.Verify(k => k.PushProductAsync(
            It.Is<KhazarProductPush>(p => p.Sku == barcode), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Khazar_failing_does_not_undo_the_receipt()
    {
        var barcode = "RCV-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var productId = await CreateProductAsync(barcode, showOnWebsite: true);
        factory.KhazarProducts.Setup(k => k.PushProductAsync(
                It.Is<KhazarProductPush>(p => p.Sku == barcode), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));

        var response = await factory.CreateClient(Tenant.StaffToken).PostJsonAsync("/api/inventory/receive",
            new { warehouseId = Tenant.WarehouseId, productId, quantity = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await factory.WithDbAsync(db => db.Stocks.Where(s => s.ProductId == productId).SumAsync(s => (double)s.Quantity))).Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_quantity_that_is_not_positive_is_refused(int quantity)
    {
        var productId = await CreateProductAsync("RCV-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), false);

        var response = await factory.CreateClient(Tenant.StaffToken).PostJsonAsync("/api/inventory/receive",
            new { warehouseId = Tenant.WarehouseId, productId, quantity });

        await response.ShouldBeErrorAsync(HttpStatusCode.BadRequest, "Say 0-dan böyük olmalıdır.");
    }

    [Fact]
    public async Task An_unknown_product_is_404()
    {
        var response = await factory.CreateClient(Tenant.StaffToken).PostJsonAsync("/api/inventory/receive",
            new { warehouseId = Tenant.WarehouseId, productId = Guid.NewGuid().ToString(), quantity = 1 });

        await response.ShouldBeErrorAsync(HttpStatusCode.NotFound, "Məhsul tapılmadı.");
    }
}
