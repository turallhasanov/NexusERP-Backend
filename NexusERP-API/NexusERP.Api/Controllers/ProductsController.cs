using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Api.Storage;
using NexusERP.Application.Products;

namespace NexusERP.Api.Controllers;

[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class ProductsController(IProductService productService, ProductImageStore imageStore) : ApiControllerBase
{
    /// <summary>All products, optionally filtered: <paramref name="q"/> searches name, code and barcode.</summary>
    [HttpGet]
    [ProducesResponseType<ItemsResponseDto<ProductDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll([FromQuery] string? q, [FromQuery] string? categoryId, CancellationToken cancellationToken) =>
        ToActionResult(await productService.GetAllAsync(CurrentCompanyId, q, categoryId, cancellationToken));

    [HttpGet("generate-barcode")]
    [ProducesResponseType<GeneratedBarcodeResponseDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateBarcode(CancellationToken cancellationToken) =>
        ToActionResult(await productService.GenerateBarcodeAsync(CurrentCompanyId, cancellationToken));

    [HttpGet("barcode/{barcode}")]
    [ProducesResponseType<ProductLookupResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByBarcode(string barcode, CancellationToken cancellationToken) =>
        ToActionResult(await productService.GetByBarcodeAsync(CurrentCompanyId, barcode, cancellationToken));

    /// <summary>
    /// Adds a product from the product form: fields plus an optional <c>image</c> file (JPEG, PNG or WebP, at most 8 MB).
    /// With <c>showOnWebsite</c> the product is also sent to the Khazar store; if that fails the product is still saved
    /// and <c>storefront</c> in the response says why.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProductImageStore.MaxFileBytes * (1 + ProductForm.MaxGalleryImages) + 64 * 1024)]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromForm] ProductForm form, CancellationToken cancellationToken)
    {
        var upload = await SaveImageAsync(form.Image, cancellationToken);
        if (upload.Error is not null)
            return BadRequest(new ErrorResponse(upload.Error));

        var gallery = await SaveGalleryAsync(form.GalleryImages, cancellationToken);
        if (gallery.Error is not null)
        {
            DeleteFiles(upload.Url, gallery.Urls);
            return BadRequest(new ErrorResponse(gallery.Error));
        }

        try
        {
            var request = new CreateProductRequestDto(
                form.CategoryId, form.UnitId, form.Name, form.Barcode, form.PurchasePrice, form.SalePrice, form.ShowOnWebsite);
            var result = await productService.CreateAsync(CurrentCompanyId, request, upload.Url, cancellationToken, gallery.Urls);

            // Refused, so the photo that came with it would only be left behind.
            if (!result.IsSuccess && upload.Url is not null)
                imageStore.TryDelete(upload.Url);

            return ToActionResult(result);
        }
        finally
        {
            // Extra photos live in the Khazar store gallery; this system keeps only the main one.
            DeleteFiles(null, gallery.Urls);
        }
    }

    /// <summary>The same as <see cref="Create"/> with a JSON body and no photo; used by other systems (Khazar).</summary>
    [HttpPost]
    [Consumes("application/json")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> CreateFromJson([FromBody] CreateProductRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await productService.CreateAsync(CurrentCompanyId, request, imageUrl: null, cancellationToken));

    /// <summary>
    /// Changes a product from the product form. A new <c>image</c> replaces the photo, <c>removeImage</c> drops it. The
    /// barcode cannot change. The Khazar store is updated when the product is, or just was, shown there.
    /// </summary>
    [HttpPut("{id}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProductImageStore.MaxFileBytes * (1 + ProductForm.MaxGalleryImages) + 64 * 1024)]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromForm] ProductForm form, CancellationToken cancellationToken)
    {
        var upload = await SaveImageAsync(form.Image, cancellationToken);
        if (upload.Error is not null)
            return BadRequest(new ErrorResponse(upload.Error));

        var gallery = await SaveGalleryAsync(form.GalleryImages, cancellationToken);
        if (gallery.Error is not null)
        {
            DeleteFiles(upload.Url, gallery.Urls);
            return BadRequest(new ErrorResponse(gallery.Error));
        }

        try
        {
            var request = new UpdateProductRequestDto(
                form.CategoryId, form.UnitId, form.Name, form.PurchasePrice, form.SalePrice, form.ShowOnWebsite);
            var result = await productService.UpdateAsync(
                CurrentCompanyId, id, request, upload.Url, form.RemoveImage, cancellationToken, gallery.Urls);

            if (!result.IsSuccess && upload.Url is not null)
                imageStore.TryDelete(upload.Url);

            return ToActionResult(result);
        }
        finally
        {
            DeleteFiles(null, gallery.Urls);
        }
    }

    // Extra photos are checked and stored like the main one, but only for the length of the request.
    private async Task<(List<string> Urls, string? Error)> SaveGalleryAsync(IFormFileCollection? files, CancellationToken cancellationToken)
    {
        var urls = new List<string>();
        var pick = files?.Where(file => file.Length > 0).ToList() ?? [];

        if (pick.Count > ProductForm.MaxGalleryImages)
            return (urls, $"Ən çox {1 + ProductForm.MaxGalleryImages} şəkil yükləmək olar.");

        foreach (var file in pick)
        {
            string? error = null;
            var url = await imageStore.SaveAsync(file, message => error = message, cancellationToken);

            if (url is null)
                return (urls, error);

            urls.Add(url);
        }

        return (urls, null);
    }

    private void DeleteFiles(string? main, IEnumerable<string> extra)
    {
        if (main is not null)
            imageStore.TryDelete(main);

        foreach (var url in extra)
            imageStore.TryDelete(url);
    }

    private async Task<(string? Url, string? Error)> SaveImageAsync(IFormFile? image, CancellationToken cancellationToken)
    {
        if (image is null || image.Length == 0)
            return (null, null);

        string? error = null;
        var url = await imageStore.SaveAsync(image, message => error = message, cancellationToken);
        return (url, error);
    }

    /// <summary>
    /// Removes the product, or deactivates it when purchases or sales reference it; 409 while a warehouse holds
    /// it. Admins and IT admins only; staff get 403.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<ProductDeleteResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken) =>
        ToActionResult(await productService.DeleteAsync(CurrentCompanyId, id, cancellationToken));
}

/// <summary>Body of POST /api/products and PUT /api/products/{id} (multipart/form-data).</summary>
public sealed class ProductForm
{
    /// <summary>Photos beyond the main <c>image</c>; the Khazar store gallery holds at most 12 in all.</summary>
    public const int MaxGalleryImages = 7;

    public string? CategoryId { get; init; }
    public string? UnitId { get; init; }
    public string? Name { get; init; }

    /// <summary>Empty on create means "generate one"; ignored on update (the barcode is the Khazar SKU).</summary>
    public string? Barcode { get; init; }

    public decimal? PurchasePrice { get; init; }
    public decimal? SalePrice { get; init; }
    public bool ShowOnWebsite { get; init; }
    public IFormFile? Image { get; init; }

    /// <summary>Further photos (<c>galleryImages</c>, repeated): sent to the Khazar store gallery, not kept here.</summary>
    public IFormFileCollection? GalleryImages { get; init; }
    public bool RemoveImage { get; init; }
}
