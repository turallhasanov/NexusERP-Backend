using System.Text.Json.Serialization;

namespace NexusERP.Application.Common;

/// <summary>Base for every API response body; <c>ok</c> is always serialized first.</summary>
public abstract record ApiResponse([property: JsonPropertyOrder(-1)] bool Ok);

/// <summary>
/// Base for success responses. Derived records add their fields at the top level:
/// <c>record CompanyResponse(CompanyDto? Company) : SuccessResponse;</c> → { "ok": true, "company": ... }
/// </summary>
public record SuccessResponse() : ApiResponse(true);

/// <summary>Error response: { "ok": false, "error": "..." } — message is in Azerbaijani.</summary>
public sealed record ErrorResponse(string Error) : ApiResponse(false);
