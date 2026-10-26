using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Products;

namespace NexusERP.Api.Controllers;

[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class ProductsController(IProductService productService) : ApiControllerBase
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

    [HttpPost]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await productService.CreateAsync(CurrentCompanyId, request, cancellationToken));

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
