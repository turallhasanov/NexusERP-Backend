using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NexusERP.Api.IntegrationTests.TestSupport;
using NexusERP.Application.Integration;

namespace NexusERP.Api.IntegrationTests.Endpoints;

/// <summary>The Khazar storefront is shared, so only a company connected to it may send products there.</summary>
public sealed class StorefrontIsolationEndpointTests(NexusErpApiFactory factory) : IClassFixture<NexusErpApiFactory>
{
    private TestTenant Tenant => factory.Tenant;

    private Task SetStorefrontAsync(string? name) =>
        factory.WithDbAsync(async db =>
            await db.Companies.Where(c => c.Id == Tenant.CompanyId).ExecuteUpdateAsync(s => s.SetProperty(c => c.StorefrontName, name)));

    private async Task<string> CreateShownProductAsync()
    {
        var barcode = "ISO-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var response = await factory.CreateClient(Tenant.ItAdminToken).PostAsJsonAsync("/api/products", new
        {
            categoryId = Tenant.CategoryId, unitId = Tenant.UnitId, name = "BBB " + barcode, barcode, salePrice = 10m, showOnWebsite = true
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return barcode;
    }

    [Fact]
    public async Task A_company_without_a_connection_does_not_reach_the_storefront()
    {
        await SetStorefrontAsync(null);
        try
        {
            var barcode = await CreateShownProductAsync();

            factory.KhazarProducts.Verify(k => k.PushProductAsync(
                It.Is<KhazarProductPush>(p => p.Sku == barcode), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            (await factory.WithDbAsync(db => db.Products.SingleAsync(p => p.Barcode == barcode))).ShowOnWebsite.Should().BeFalse();
        }
        finally { await SetStorefrontAsync("Khazar"); }
    }

    [Fact]
    public async Task A_connected_company_still_syncs()
    {
        var barcode = await CreateShownProductAsync();

        factory.KhazarProducts.Verify(k => k.PushProductAsync(
            It.Is<KhazarProductPush>(p => p.Sku == barcode && p.IsPublished), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_company_endpoint_returns_the_storefront_name_or_null()
    {
        var connected = await (await factory.CreateClient(Tenant.ItAdminToken).GetAsync("/api/company")).JsonAsync();
        connected["company"]!["storefrontName"]!.GetValue<string>().Should().Be("Khazar");

        await SetStorefrontAsync(null);
        try
        {
            var none = await (await factory.CreateClient(Tenant.ItAdminToken).GetAsync("/api/company")).JsonAsync();
            none["company"]!["storefrontName"].Should().BeNull();
        }
        finally { await SetStorefrontAsync("Khazar"); }
    }
}
