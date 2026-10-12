using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Products;

namespace NexusERP.Api.Controllers;

[Route("api/products")]
[Authorize, RequireCompany]
public class ProductsController(IProductService productService) : ApiControllerBase
{
    /// <summary>All products, optionally filtered: <paramref name="q"/> searches name, code and barcode.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? q, [FromQuery] string? categoryId, CancellationToken cancellationToken) =>
        ToActionResult(await productService.GetAllAsync(CurrentCompanyId, q, categoryId, cancellationToken));

    [HttpGet("generate-barcode")]
    public async Task<IActionResult> GenerateBarcode(CancellationToken cancellationToken) =>
        ToActionResult(await productService.GenerateBarcodeAsync(CurrentCompanyId, cancellationToken));

    [HttpGet("barcode/{barcode}")]
    public async Task<IActionResult> GetByBarcode(string barcode, CancellationToken cancellationToken) =>
        ToActionResult(await productService.GetByBarcodeAsync(CurrentCompanyId, barcode, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await productService.CreateAsync(CurrentCompanyId, request, cancellationToken));
}
