using System.Net;
using System.Net.Http.Headers;
using NexusERP.Api.IntegrationTests.TestSupport;
using NexusERP.Application.Integration;

namespace NexusERP.Api.IntegrationTests.Endpoints;

/// <summary>POST /api/products with no photo, one photo, or several: only the main photo is kept, the rest go to Khazar.</summary>
public sealed class ProductGalleryEndpointTests(NexusErpApiFactory factory) : IClassFixture<NexusErpApiFactory>
{
    private TestTenant Tenant => factory.Tenant;

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private MultipartFormDataContent Form(string barcode, bool showOnWebsite, int photos, bool mainPhoto = true)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(Tenant.CategoryId), "categoryId" },
            { new StringContent(Tenant.UnitId), "unitId" },
            { new StringContent("Galereya paltosu"), "name" },
            { new StringContent(barcode), "barcode" },
            { new StringContent("59.90"), "salePrice" },
            { new StringContent(showOnWebsite ? "true" : "false"), "showOnWebsite" },
        };

        for (var i = 0; i < photos; i++)
        {
            var content = new ByteArrayContent(PngBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(content, mainPhoto && i == 0 ? "image" : "galleryImages", $"photo{i}.png");
        }

        return form;
    }

    private static string NewBarcode() => "GAL-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    [Fact]
    public async Task A_product_without_any_photo_is_saved_and_sent_to_khazar_without_one()
    {
        var barcode = NewBarcode();

        var response = await factory.CreateClient(Tenant.StaffToken).PostAsync("/api/products", Form(barcode, showOnWebsite: true, photos: 0));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.JsonAsync())["imageUrl"].Should().BeNull();
        factory.KhazarProducts.Verify(k => k.PushProductAsync(
            It.Is<KhazarProductPush>(p => p.Sku == barcode && (p.Gallery == null || p.Gallery.Count == 0)),
            null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Three_photos_keep_the_first_here_and_send_all_three_to_khazar()
    {
        var barcode = NewBarcode();
        Stream? sentMain = null;
        KhazarProductPush? sent = null;
        factory.KhazarProducts
            .Setup(k => k.PushProductAsync(It.Is<KhazarProductPush>(p => p.Sku == barcode), It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<KhazarProductPush, Stream?, string?, CancellationToken>((push, stream, _, _) =>
            {
                sent = push;
                sentMain = stream is null ? null : new MemoryStream();
            })
            .ReturnsAsync(KhazarProductResult.Ok);

        var response = await factory.CreateClient(Tenant.StaffToken).PostAsync("/api/products", Form(barcode, showOnWebsite: true, photos: 3));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.JsonAsync())["imageUrl"]!.GetValue<string>().Should().StartWith("/uploads/products/");
        sentMain.Should().NotBeNull("the main photo travels as the image part");
        sent!.Gallery.Should().HaveCount(2);
        sent.Gallery!.Select(g => g.Content.Length).Should().AllBeEquivalentTo(PngBytes.Length);
    }

    [Fact]
    public async Task Gallery_photos_are_not_kept_on_disk_after_the_request()
    {
        var before = Directory.GetFiles(Path.Combine(factory.UploadsRoot, "products")).Length;

        await factory.CreateClient(Tenant.StaffToken).PostAsync("/api/products", Form(NewBarcode(), showOnWebsite: true, photos: 4));

        // Only the main photo stays.
        Directory.GetFiles(Path.Combine(factory.UploadsRoot, "products")).Length.Should().Be(before + 1);
    }

    [Fact]
    public async Task More_than_eight_photos_are_refused()
    {
        var response = await factory.CreateClient(Tenant.StaffToken).PostAsync("/api/products", Form(NewBarcode(), showOnWebsite: false, photos: 9));

        await response.ShouldBeErrorAsync(HttpStatusCode.BadRequest, "Ən çox 8 şəkil yükləmək olar.");
    }

    [Fact]
    public async Task A_gallery_file_that_is_not_an_image_is_refused()
    {
        var form = Form(NewBarcode(), showOnWebsite: false, photos: 0);
        var bad = new ByteArrayContent("not an image at all"u8.ToArray());
        bad.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(bad, "galleryImages", "fake.png");

        var response = await factory.CreateClient(Tenant.StaffToken).PostAsync("/api/products", form);

        await response.ShouldBeErrorAsync(HttpStatusCode.BadRequest, "Yalnız JPEG, PNG və WebP şəkilləri qəbul olunur.");
    }
}
