using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NexusERP.Application.Integration;

namespace NexusERP.Infrastructure.Integration;

public sealed class KhazarProductClient(HttpClient httpClient, IOptions<KhazarSettings> options, ILogger<KhazarProductClient> logger)
    : IKhazarProductClient
{
    private const string ProductsEndpoint = "api/integration/products";

    public async Task<KhazarProductResult> PushProductAsync(
        KhazarProductPush product, Stream? image, string? imageFileName, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!settings.HasBaseUrl)
            return Fail(product, "Khazar:BaseUrl ayarı təyin olunmayıb və ya düzgün URL deyil.");

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return Fail(product, "Khazar:ApiKey ayarı təyin olunmayıb.");

        try
        {
            using var form = new MultipartFormDataContent
            {
                { new StringContent(product.Sku), "sku" },
                { new StringContent(product.Name), "name" },
                { new StringContent(product.SalePrice.ToString(CultureInfo.InvariantCulture)), "salePrice" },
                { new StringContent(product.Stock.ToString(CultureInfo.InvariantCulture)), "stock" },
                { new StringContent(product.IsPublished ? "true" : "false"), "isPublished" },
            };

            if (!string.IsNullOrWhiteSpace(product.CategoryName))
                form.Add(new StringContent(product.CategoryName), "categoryName");

            if (image is not null)
            {
                var file = new StreamContent(image);
                file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(file, "image", imageFileName ?? "image");
            }

            foreach (var extra in product.Gallery ?? [])
            {
                var extraFile = new ByteArrayContent(extra.Content);
                extraFile.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(extraFile, "galleryImages", extra.FileName);
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, ProductsEndpoint) { Content = form };
            request.Headers.Add("X-Api-Key", settings.ApiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Product {Sku} was sent to Khazar ({Published}), stock {Stock}: HTTP {StatusCode}.",
                    product.Sku, product.IsPublished ? "shown" : "hidden", product.Stock, (int)response.StatusCode);
                return KhazarProductResult.Ok;
            }

            logger.LogWarning(
                "Khazar answered HTTP {StatusCode} for product {Sku} (stock {Stock}).",
                (int)response.StatusCode, product.Sku, product.Stock);

            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => Fail(product,
                    "Khazar ApiKey-i qəbul etmədi: NexusERP-dəki Khazar:ApiKey Khazar-dakı Integration:ApiKey ilə eyni olmalıdır."),
                HttpStatusCode.NotFound => Fail(product,
                    "Khazar-da Integration:ApiKey təyin olunmayıb və ya Khazar API köhnə versiyadır (məhsul endpoint-i tapılmadı)."),
                HttpStatusCode.BadRequest => Fail(product, $"Khazar məhsulu qəbul etmədi: {await ReadProblemAsync(response, cancellationToken)}"),
                _ => Fail(product, $"Khazar məhsulu qəbul etmədi (HTTP {(int)response.StatusCode})."),
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or NotSupportedException
                                       || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Product {Sku} could not be sent to Khazar at {BaseUrl}.", product.Sku, settings.BaseUrl);
            return KhazarProductResult.Failed(
                $"Khazar API-yə qoşulmaq mümkün olmadı ({settings.BaseUrl}). Khazar API-nin işlədiyini yoxlayın.");
        }
    }

    // Khazar answers validation failures as RFC 7807 ProblemDetails; "detail" says which field.
    private static async Task<string> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (document.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                return detail.GetString()!;
        }
        catch (JsonException)
        {
            // Not JSON: fall through.
        }

        return "səbəb göstərilməyib";
    }

    private KhazarProductResult Fail(KhazarProductPush product, string message)
    {
        logger.LogWarning("Product {Sku} was not sent to Khazar: {Reason}", product.Sku, message);
        return KhazarProductResult.Failed(message);
    }
}
