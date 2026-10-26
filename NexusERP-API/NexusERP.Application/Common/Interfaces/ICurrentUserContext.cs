namespace NexusERP.Application.Common.Interfaces;

/// <summary>Who is making the current request, for the audit trail. Values are null outside an HTTP request.</summary>
public interface ICurrentUserContext
{
    string? UserId { get; }
    string? IpAddress { get; }
}
