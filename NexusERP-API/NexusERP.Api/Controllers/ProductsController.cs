using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Products;

namespace NexusERP.Api.Controllers;

[Route("api/products")]
[Authorize, RequireCompany]
public class ProductsController(IProductService productService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await productService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpGet("barcode/{barcode}")]
    public async Task<IActionResult> GetByBarcode(string barcode, CancellationToken cancellationToken) =>
        ToActionResult(await productService.GetByBarcodeAsync(CurrentCompanyId, barcode, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await productService.CreateAsync(CurrentCompanyId, request, cancellationToken));
}
