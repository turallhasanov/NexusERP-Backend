using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexusERP.Api.ExceptionHandling;

namespace NexusERP.Api.IntegrationTests.ExceptionHandling;

/// <summary>The handler on its own: the Development-only details, the 499 for an aborted request and BadHttpRequest.</summary>
public sealed class GlobalExceptionHandlerTests
{
    private static async Task<(HttpContext Context, JsonObject? Body, bool Handled)> HandleAsync(
        Exception exception, string environmentName, Action<HttpContext>? arrange = null)
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = "POST";
        context.Request.Path = "/api/sales";
        context.Response.Body = new MemoryStream();
        arrange?.Invoke(context);

        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);

        var handler = new GlobalExceptionHandler(
            services.GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>(),
            environment.Object,
            NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context, text.Length == 0 ? null : JsonNode.Parse(text)!.AsObject(), handled);
    }

    [Fact]
    public async Task Development_adds_the_exception_type_and_message_but_no_stack_trace()
    {
        var (context, body, handled) = await HandleAsync(new InvalidOperationException("Sequence contains no elements"), Environments.Development);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(500);
        body!["exception"]!["type"]!.GetValue<string>().Should().Be("System.InvalidOperationException");
        body["exception"]!["message"]!.GetValue<string>().Should().Be("Sequence contains no elements");
        body.ToJsonString().Should().NotContain("stackTrace").And.NotContain("   at ");
        body["instance"]!.GetValue<string>().Should().Be("POST /api/sales");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Other_environments_get_no_exception_details(string environmentName)
    {
        var (_, body, _) = await HandleAsync(new InvalidOperationException("internal detail"), environmentName);

        body!.ContainsKey("exception").Should().BeFalse();
        body.ToJsonString().Should().NotContain("internal detail");
        body["ok"]!.GetValue<bool>().Should().BeFalse();
        body["error"]!.GetValue<string>().Should().Be(body["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_request_the_client_aborted_is_499_with_no_body()
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();

        var (context, body, handled) = await HandleAsync(new OperationCanceledException(), "Production",
            c => c.RequestAborted = aborted.Token);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(499);
        body.Should().BeNull();
    }

    [Fact]
    public async Task A_cancellation_the_client_did_not_ask_for_is_a_500()
    {
        // E.g. a database command timeout: the request is still alive, so it is a server failure.
        var (context, body, _) = await HandleAsync(new OperationCanceledException(), "Production");

        context.Response.StatusCode.Should().Be(500);
        body!["status"]!.GetValue<int>().Should().Be(500);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(413)]
    public async Task A_malformed_request_keeps_its_own_status_and_the_ErrorResponse_shape(int status)
    {
        var (context, body, _) = await HandleAsync(new BadHttpRequestException("Request body too large.", status), "Production");

        context.Response.StatusCode.Should().Be(status);
        body!["ok"]!.GetValue<bool>().Should().BeFalse();
        body["error"]!.GetValue<string>().Should().Be("Sorğu formatı yanlışdır.");
        body.ContainsKey("traceId").Should().BeFalse();
    }

    [Fact]
    public async Task The_failure_is_logged_once_as_an_error_with_the_trace_id()
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "trace-123" };
        context.Response.Body = new MemoryStream();
        var logger = new Mock<ILogger<GlobalExceptionHandler>>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var environment = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == "Production");

        await new GlobalExceptionHandler(services.GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>(), environment, logger.Object)
            .TryHandleAsync(context, new TimeoutException("db"), CancellationToken.None);

        logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("trace-123") && state.ToString()!.Contains("TimeoutException")),
            It.IsAny<TimeoutException>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
