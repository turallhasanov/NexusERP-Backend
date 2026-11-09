using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NexusERP.Api.IntegrationTests.TestSupport;
using NexusERP.Application.Integration;

namespace NexusERP.Api.IntegrationTests.Endpoints;

/// <summary>POST /api/products: the product form (multipart, optional photo) and the JSON body Khazar sends.</summary>
public sealed class ProductsEndpointTests(NexusErpApiFactory factory) : IClassFixture<NexusErpApiFactory>
{
    private TestTenant Tenant => factory.Tenant;

    // The 8-byte PNG signature plus a little padding: enough for the store's content sniffing.
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private MultipartFormDataContent ProductForm(string name, string? barcode = null, bool showOnWebsite = false, (byte[] Bytes, string FileName)? image = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(Tenant.CategoryId), "categoryId" },
            { new StringContent(Tenant.UnitId), "unitId" },
            { new StringContent(name), "name" },
            { new StringContent("25.5"), "purchasePrice" },
            { new StringContent("59.90"), "salePrice" },
            { new StringContent(showOnWebsite ? "true" : "false"), "showOnWebsite" },
        };

        if (barcode is not null)
            form.Add(new StringContent(barcode), "barcode");

        if (image is { } file)
        {
            var content = new ByteArrayContent(file.Bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(content, "image", file.FileName);
        }

        return form;
    }

    [Fact]
    public async Task The_product_form_creates_a_numbered_product_with_a_generated_barcode()
    {
        var response = await factory.CreateClient(Tenant.StaffToken).PostAsync("/api/products", ProductForm("Yun palto"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.JsonAsync();
        body["ok"]!.GetValue<bool>().Should().BeTrue();
        body["code"]!.GetValue<string>().Should().StartWith("MHS-");
        body["name"]!.GetValue<string>().Should().Be("Yun palto");
        body["barcode"]!.GetValue<string>().Should().MatchRegex("^[0-9]{13}$");
        body["salePrice"]!.GetValue<decimal>().Should().Be(59.90m);
        body["category"]!["id"]!.GetValue<string>().Should().Be(Tenant.CategoryId);
        body["unit"]!["shortName"]!.GetValue<string>().Should().Be("əd.");
        body["imageUrl"].Should().BeNull();
        body["storefront"]!["status"]!.GetValue<string>().Should().Be("notShown");
    }

    [Fact]
    public async Task A_photo_is_stored_and_served_from_the_uploads_folder()
    {
        var response = await factory.CreateClient(Tenant.StaffToken)
            .PostAsync("/api/products", ProductForm("Şəkilli şərf", image: (PngBytes, "sharf.png")));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var imageUrl = (await response.JsonAsync())["imageUrl"]!.GetValue<string>();
        imageUrl.Should().MatchRegex("^/uploads/products/[0-9a-f]{32}\\.png$");

        File.Exists(Path.Combine(factory.UploadsRoot, "products", Path.GetFileName(imageUrl))).Should().BeTrue();
        var served = await factory.CreateClient().GetAsync(imageUrl);
        served.StatusCode.Should().Be(HttpStatusCode.OK);
        (await served.Content.ReadAsByteArrayAsync()).Should().Equal(PngBytes);
    }

    [Fact]
    public async Task A_file_that_is_not_an_image_is_refused_with_400()
    {
        var response = await factory.CreateClient(Tenant.StaffToken)
            .PostAsync("/api/products", ProductForm("Saxta şəkil", image: ("not an image"u8.ToArray(), "evil.png")));

        await response.ShouldBeErrorAsync(HttpStatusCode.BadRequest, "Yalnız JPEG, PNG və WebP şəkilləri qəbul olunur.");
    }

    [Fact]
    public async Task A_duplicate_barcode_is_409()
    {
        var client = factory.CreateClient(Tenant.StaffToken);
        var barcode = "DUP-" + Guid.NewGuid().ToString("N")[..8];
        (await client.PostAsync("/api/products", ProductForm("Birinci", barcode))).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.PostAsync("/api/products", ProductForm("İkinci", barcode));

        await response.ShouldBeErrorAsync(HttpStatusCode.Conflict, "Bu barkod artıq başqa məhsulda istifadə olunur.");
    }

    [Fact]
    public async Task Show_on_website_pushes_the_product_to_khazar()
    {
        var barcode = "WEB-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var response = await factory.CreateClient(Tenant.StaffToken).PostAsync("/api/products", ProductForm("Sayt palto", barcode, showOnWebsite: true));

        (await response.JsonAsync())["storefront"]!["status"]!.GetValue<string>().Should().Be("synced");
        factory.KhazarProducts.Verify(k => k.PushProductAsync(
            It.Is<KhazarProductPush>(p => p.Sku == barcode && p.Name == "Sayt palto" && p.IsPublished && p.Stock == 0 && p.CategoryName == "Palto"),
            It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_json_body_khazar_sends_creates_the_product_too()
    {
        var barcode = "KHZ-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var response = await factory.CreateClient(Tenant.ItAdminToken).PostAsJsonAsync("/api/products", new
        {
            categoryId = Tenant.CategoryId, unitId = Tenant.UnitId, name = "Palto (Qara / M)", barcode, salePrice = 120m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.JsonAsync();
        body["id"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        body["barcode"]!.GetValue<string>().Should().Be(barcode);
        (await factory.WithDbAsync(db => db.Products.AnyAsync(p => p.Barcode == barcode))).Should().BeTrue();
    }

    [Fact]
    public async Task A_form_without_a_name_is_400()
    {
        var response = await factory.CreateClient(Tenant.StaffToken).PostAsync("/api/products", ProductForm(" "));

        await response.ShouldBeErrorAsync(HttpStatusCode.BadRequest, "Məhsul adı tələb olunur.");
    }

    [Fact]
    public async Task Without_a_token_it_is_401()
    {
        var response = await factory.CreateClient(token: null).PostAsync("/api/products", ProductForm("X"));

        await response.ShouldBeErrorAsync(HttpStatusCode.Unauthorized, "Giriş tələb olunur.");
    }
}
