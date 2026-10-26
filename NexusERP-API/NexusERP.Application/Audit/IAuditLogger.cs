using NexusERP.Domain.Enums;

namespace NexusERP.Application.Audit;

/// <summary>Writes audit trail entries; never fails the action being logged.</summary>
public interface IAuditLogger
{
    /// <summary>
    /// Records that the current user did <paramref name="what"/> (e.g. "'ALS-001' alış qaiməsi təsdiqləndi"); the
    /// entry reads "{name} tərəfindən {what}". Call after the action has been saved.
    /// </summary>
    Task LogAsync(string companyId, AuditModule module, AuditAction action, string what, CancellationToken cancellationToken);
}
