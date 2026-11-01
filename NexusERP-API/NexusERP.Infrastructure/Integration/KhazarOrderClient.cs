using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NexusERP.Application.Integration;

namespace NexusERP.Infrastructure.Integration;

public sealed class KhazarOrderClient(HttpClient httpClient, IOptions<KhazarSettings> options, ILogger<KhazarOrderClient> logger)
    : IKhazarOrderClient
{
    private const string OrdersEndpoint = "api/integration/orders/";

    public async Task<KhazarCancelResult> CancelOrderAsync(string orderNumber, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!settings.HasBaseUrl)
            return Fail("Khazar:BaseUrl ayarı təyin olunmayıb və ya düzgün URL deyil.");

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return Fail("Khazar:ApiKey ayarı təyin olunmayıb.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, OrdersEndpoint + Uri.EscapeDataString(orderNumber));
            request.Headers.Add("X-Api-Key", settings.ApiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Order {OrderNumber} was cancelled in Khazar.", orderNumber);
                return KhazarCancelResult.Ok;
            }

            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    return Fail("Khazar ApiKey-i qəbul etmədi: NexusERP-dəki Khazar:ApiKey Khazar-dakı Integration:ApiKey ilə eyni olmalıdır.");
                case HttpStatusCode.Conflict:
                    return Fail($"Khazar-da {orderNumber} sifarişi artıq tamamlanıb (təhvil verilib), ləğv etmək olmaz.");
                case HttpStatusCode.NotFound:
                    // Khazar's own "order not found" answer carries an errorCode; a bare 404 means the route or the key
                    // setting is missing on Khazar, which is a failure, not "nothing to cancel".
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (body.Contains("\"not_found\"", StringComparison.Ordinal))
                    {
                        logger.LogWarning("Khazar has no order {OrderNumber}; nothing to cancel there.", orderNumber);
                        return KhazarCancelResult.Ok;
                    }

                    return Fail("Khazar-da Integration:ApiKey təyin olunmayıb və ya Khazar API köhnə versiyadır (ləğv endpoint-i tapılmadı).");
                default:
                    return Fail($"Khazar sifarişi ləğv etmədi (HTTP {(int)response.StatusCode}).");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or NotSupportedException
                                       || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Order {OrderNumber} could not be cancelled in Khazar at {BaseUrl}.", orderNumber, settings.BaseUrl);
            return KhazarCancelResult.Failed(
                $"Khazar API-yə qoşulmaq mümkün olmadı ({settings.BaseUrl}). Khazar API-nin işlədiyini yoxlayın.");
        }

        KhazarCancelResult Fail(string message)
        {
            logger.LogWarning("Order {OrderNumber} was not cancelled in Khazar: {Reason}", orderNumber, message);
            return KhazarCancelResult.Failed(message);
        }
    }
}
