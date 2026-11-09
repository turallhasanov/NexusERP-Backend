using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NexusERP.Api.IntegrationTests.TestSupport;
using NexusERP.Application.Customers;

namespace NexusERP.Api.IntegrationTests.ExceptionHandling;

/// <summary>
/// An exception nothing catches, thrown inside a real request: the GlobalExceptionHandler must turn it into the RFC 7807
/// 500 (problem+json with traceId, ok: false and error), keep CORS headers on it and leak nothing about the cause.
/// </summary>
public sealed class UnhandledExceptionPipelineTests(NexusErpApiFactory factory) : IClassFixture<NexusErpApiFactory>
{
    private const string SecretCause = "Login failed for user 'sa' on Server=PROD-SQL;Password=hunter2";
    private const string Title = "An error occurred while processing your request.";
    private const string Detail = "Gözlənilməz xəta baş verdi. Bir az sonra yenidən cəhd edin; problem davam edərsə, trace id-ni dəstəyə göndərin.";

    /// <summary>The API with a counterparty service whose list call blows up the way a dropped SQL connection would.</summary>
    private HttpClient ClientWithFailingService(string? accept = null)
    {
        var failing = new Mock<ICustomerService>();
        failing.Setup(s => s.GetAllAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(SecretCause));

        var client = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerService>();
            services.AddScoped(_ => failing.Object);
        })).CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Tenant.AdminToken);
        if (accept is not null)
            client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        return client;
    }

    [Fact]
    public async Task An_unhandled_exception_becomes_an_RFC7807_500()
    {
        var response = await ClientWithFailingService().GetAsync("/api/customers");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await response.JsonAsync();
        body["status"]!.GetValue<int>().Should().Be(500);
        body["title"]!.GetValue<string>().Should().Be(Title);
        body["detail"]!.GetValue<string>().Should().Be(Detail);
        body["instance"]!.GetValue<string>().Should().Be("GET /api/customers");
        body["type"]!.GetValue<string>().Should().Be("https://tools.ietf.org/html/rfc9110#section-15.6.1");
        body["traceId"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();

        // ErrorResponse fields, so the ERP frontend (response.data.error) and Khazar (ErrorResponse.Error) show the message.
        body["ok"]!.GetValue<bool>().Should().BeFalse();
        body["error"]!.GetValue<string>().Should().Be(Detail);
    }

    [Fact]
    public async Task Nothing_about_the_cause_reaches_the_client_outside_development()
    {
        var response = await ClientWithFailingService().GetAsync("/api/customers");
        var text = await response.Content.ReadAsStringAsync();

        text.Should().NotContain("hunter2").And.NotContain("PROD-SQL").And.NotContain(nameof(InvalidOperationException))
            .And.NotContain("   at ");
        (await response.JsonAsync()).ContainsKey("exception").Should().BeFalse();
    }

    [Fact]
    public async Task The_500_keeps_CORS_headers_so_the_browser_can_read_it()
    {
        var client = ClientWithFailingService();
        client.DefaultRequestHeaders.Add("Origin", NexusErpApiFactory.AllowedOrigin);

        var response = await client.GetAsync("/api/customers");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal(NexusErpApiFactory.AllowedOrigin);
    }

    [Fact]
    public async Task The_500_is_json_even_when_the_client_asks_for_html()
    {
        var response = await ClientWithFailingService(accept: "text/html").GetAsync("/api/customers");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.JsonAsync())["traceId"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Each_failure_gets_its_own_trace_id()
    {
        var client = ClientWithFailingService();

        var first = (await (await client.GetAsync("/api/customers")).JsonAsync())["traceId"]!.GetValue<string>();
        var second = (await (await client.GetAsync("/api/customers")).JsonAsync())["traceId"]!.GetValue<string>();

        first.Should().NotBe(second);
    }

    [Fact]
    public async Task Ordinary_errors_keep_their_status_and_ErrorResponse_shape()
    {
        // Only exceptions go to the handler: Result failures and auth answers are unchanged.
        var client = factory.CreateClient(factory.Tenant.AdminToken);

        await (await client.DeleteAsync($"/api/customers/{Guid.NewGuid()}"))
            .ShouldBeErrorAsync(HttpStatusCode.NotFound, "Kontragent tapılmadı.");
        await (await factory.CreateClient(token: null).GetAsync("/api/customers"))
            .ShouldBeErrorAsync(HttpStatusCode.Unauthorized, "Giriş tələb olunur.");

        var unknownRoute = await client.GetAsync("/api/no-such-endpoint");
        unknownRoute.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await unknownRoute.Content.ReadAsStringAsync()).Should().BeEmpty(); // no UseStatusCodePages: still body-less
    }

    [Fact]
    public async Task Malformed_json_stays_a_400_ErrorResponse()
    {
        var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var response = await factory.CreateClient(factory.Tenant.AdminToken).PostAsync("/api/customers", content);

        await response.ShouldBeErrorAsync(HttpStatusCode.BadRequest, "Sorğu formatı yanlışdır.");
    }
}
