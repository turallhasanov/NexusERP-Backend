using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NexusERP.Application.Integration;

namespace NexusERP.Infrastructure.Integration;

/// <summary>Settings bound from the "Khazar" configuration section.</summary>
public sealed class KhazarSettings
{
    public const string SectionName = "Khazar";

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Shared secret for Khazar's GET api/integration/catalog (header X-Api-Key); must equal Khazar's Integration:ApiKey.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    public bool HasBaseUrl => Uri.TryCreate(BaseUrl, UriKind.Absolute, out _);
}

public sealed class KhazarCatalogClient(HttpClient httpClient, IOptions<KhazarSettings> options, ILogger<KhazarCatalogClient> logger)
    : IKhazarCatalogClient
{
    private const string HealthEndpoint = "health";
    private const string CatalogEndpoint = "api/integration/catalog";

    public async Task<KhazarCatalogResult> GetCatalogAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!settings.HasBaseUrl)
            return Fail("Khazar:BaseUrl ayarı təyin olunmayıb və ya düzgün URL deyil.");

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return Fail("Khazar:ApiKey ayarı təyin olunmayıb.");

        try
        {
            // Health first, so "Khazar is down" and "Khazar refused the key" are told apart.
            using (var health = await httpClient.GetAsync(HealthEndpoint, cancellationToken))
            {
                if (health.StatusCode == HttpStatusCode.ServiceUnavailable)
                    return Fail($"Khazar API işləyir, amma sağlam deyil (verilənlər bazası?). Ünvan: {settings.BaseUrl}");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, CatalogEndpoint);
            request.Headers.Add("X-Api-Key", settings.ApiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    var items = await response.Content.ReadFromJsonAsync<List<KhazarCatalogItem>>(cancellationToken);
                    return items is null
                        ? Fail("Khazar kataloqu boş cavab qaytardı.")
                        : new KhazarCatalogResult(
                            items.Select(item => item with { ImageUrl = AbsoluteImageUrl(item.ImageUrl, settings.BaseUrl) }).ToList(),
                            null);
                case HttpStatusCode.Unauthorized:
                    return Fail("Khazar ApiKey-i qəbul etmədi: NexusERP-dəki Khazar:ApiKey Khazar-dakı Integration:ApiKey ilə eyni olmalıdır.");
                case HttpStatusCode.NotFound:
                    return Fail("Khazar-da Integration:ApiKey təyin olunmayıb (inteqrasiya endpoint-i söndürülüb) və ya Khazar API köhnə versiyadır.");
                default:
                    return Fail($"Khazar kataloqu HTTP {(int)response.StatusCode} qaytardı.");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or NotSupportedException
                                       || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Khazar catalog could not be read from {BaseUrl}.", settings.BaseUrl);
            return new KhazarCatalogResult(null,
                $"Khazar API-yə qoşulmaq mümkün olmadı ({settings.BaseUrl}). Khazar API-nin işlədiyini yoxlayın.");
        }

        // Khazar sends its photos as site paths (/images/x.jpg, /uploads/products/x.png) that only mean something on its
        // own address. Anything else (its placeholder /favicon.svg, an empty value) is not a product photo.
        static string? AbsoluteImageUrl(string? path, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            path = path.Trim();

            if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
                return absolute.Scheme is "http" or "https" ? path : null;

            return path.StartsWith("/images/", StringComparison.Ordinal) || path.StartsWith("/uploads/", StringComparison.Ordinal)
                ? baseUrl.TrimEnd('/') + path
                : null;
        }

        KhazarCatalogResult Fail(string message)
        {
            logger.LogWarning("Khazar catalog was not read: {Reason}", message);
            return new KhazarCatalogResult(null, message);
        }
    }
}
