using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace NexusERP.Api.ExceptionHandling;

/// <summary>
/// Last line of defence for exceptions nothing else caught. Expected failures never get here: services return
/// <c>Result</c> failures, which controllers turn into <c>{ "ok": false, "error": "..." }</c> with their own status.
/// An unhandled exception is logged in full with a trace id, and the client gets an RFC 7807 ProblemDetails 500 that
/// carries only that trace id, never the stack trace, SQL or connection details.
/// </summary>
/// <remarks>
/// The body also carries <c>ok: false</c> and <c>error</c>, the fields of <c>ErrorResponse</c>: the ERP frontend
/// (<c>response.data.error</c>) and Khazar's NexusErpClient (<c>ErrorResponse.Error</c>) read the message from there.
/// </remarks>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    internal const string Title = "An error occurred while processing your request.";
    internal const string Detail = "Gözlənilməz xəta baş verdi. Bir az sonra yenidən cəhd edin; problem davam edərsə, trace id-ni dəstəyə göndərin.";

    // Non-standard but widely used (nginx) status for "client closed the request".
    private const int ClientClosedRequest = 499;

    private const string ProblemJsonContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        var request = httpContext.Request;

        // The caller is gone: nobody to answer and nothing went wrong on the server.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request {Method} {Path} was cancelled by the client.", request.Method, request.Path);
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        // A malformed request (e.g. a body over the size limit). Without this handler Kestrel answers it with the
        // exception's own status (400, 413, ...); keep that status and our usual error shape instead of a 500.
        if (exception is BadHttpRequestException badRequest)
        {
            logger.LogWarning("Request {Method} {Path} was rejected with {StatusCode}: {Message}",
                request.Method, request.Path, badRequest.StatusCode, badRequest.Message);
            httpContext.Response.StatusCode = badRequest.StatusCode;
            await httpContext.Response.WriteAsJsonAsync(new { ok = false, error = "Sorğu formatı yanlışdır." }, cancellationToken);
            return true;
        }

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = traceId,
            ["RequestMethod"] = request.Method,
            ["RequestPath"] = request.Path.Value,
            ["ExceptionType"] = exception.GetType().FullName,
            ["UserId"] = httpContext.User.FindFirst("sub")?.Value,
            ["CompanyId"] = httpContext.User.FindFirst("companyId")?.Value,
        }))
        {
            logger.LogError(exception,
                "Unhandled {ExceptionType} while processing {Method} {Path}{QueryString} (trace id {TraceId}).",
                exception.GetType().Name, request.Method, request.Path, request.QueryString, traceId);
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = Title,
            Detail = Detail,
            Instance = $"{request.Method} {request.Path}",
        };

        problem.Extensions["traceId"] = traceId;
        problem.Extensions["ok"] = false;
        problem.Extensions["error"] = Detail;

        // Developers get the real cause locally (type and message, no stack trace); other environments never do.
        if (environment.IsDevelopment())
            problem.Extensions["exception"] = new { type = exception.GetType().FullName, message = exception.Message };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        try
        {
            var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problem,
                Exception = exception,
            });

            // TryWriteAsync declines when the Accept header excludes JSON (e.g. text/html); write the JSON anyway.
            if (!written)
                await httpContext.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null,
                    ProblemJsonContentType, cancellationToken);
        }
        catch (Exception writeException)
        {
            // Returning true still stops the original exception from being rethrown; the 500 status is already set.
            logger.LogError(writeException, "Failed to write the error response for trace id {TraceId}.", traceId);
        }

        return true;
    }
}
