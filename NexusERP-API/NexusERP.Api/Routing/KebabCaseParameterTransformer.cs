using System.Text.RegularExpressions;

namespace NexusERP.Api.Routing;

/// <summary>
/// Turns route tokens such as [controller] into lowercase kebab-case, so CashRegistersController is routed
/// and documented in Swagger as /api/cash-registers instead of /api/CashRegisters.
/// </summary>
public sealed partial class KebabCaseParameterTransformer : IOutboundParameterTransformer
{
    public string? TransformOutbound(object? value) =>
        value is null ? null : WordBoundary().Replace(value.ToString()!, "$1-$2").ToLowerInvariant();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex WordBoundary();
}
