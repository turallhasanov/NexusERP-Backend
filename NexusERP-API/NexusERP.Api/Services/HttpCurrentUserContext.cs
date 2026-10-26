using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;

namespace NexusERP.Api.Services;

/// <summary>The signed-in user ("sub" claim) and client IP of the current HTTP request.</summary>
public sealed class HttpCurrentUserContext(IHttpContextAccessor accessor) : ICurrentUserContext
{
    public string? UserId => accessor.HttpContext?.User.FindFirst(AppClaimTypes.UserId)?.Value;

    // The direct peer; behind a reverse proxy that would be the proxy unless forwarded headers are configured.
    // IPv4 clients often arrive as "::ffff:1.2.3.4": shown as plain IPv4. Real IPv6 (e.g. "::1") stays as it is.
    public string? IpAddress
    {
        get
        {
            var address = accessor.HttpContext?.Connection.RemoteIpAddress;
            return address is null ? null : (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
        }
    }
}
