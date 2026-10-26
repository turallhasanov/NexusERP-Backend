using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Audit;

public class AuditLogger(IApplicationDbContext db, ICurrentUserContext currentUser, ILogger<AuditLogger> logger) : IAuditLogger
{
    // Must match AuditLogConfiguration.
    private const int DescriptionMaxLength = 1000;

    public static string DepartmentOf(AuditModule module) => module switch
    {
        AuditModule.Finance => "Maliyyə",
        AuditModule.HR => "İHR",
        AuditModule.Inventory => "Anbar",
        _ => "Şəbəkə"
    };

    public async Task LogAsync(string companyId, AuditModule module, AuditAction action, string what, CancellationToken cancellationToken)
    {
        var entry = new AuditLog { CompanyId = companyId, ModuleName = module, ActionType = action, Department = DepartmentOf(module) };

        try
        {
            var userId = currentUser.UserId;
            var user = userId is null
                ? null
                : await db.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new
                    {
                        u.Name,
                        u.Role,
                        CustomRole = db.CustomRoles.Where(r => r.Id == u.CustomRoleId).Select(r => r.Name).FirstOrDefault()
                    })
                    .FirstOrDefaultAsync(cancellationToken);

            var name = user?.Name ?? "Sistem";
            var description = $"{name} tərəfindən {what}";

            entry.UserId = userId;
            entry.UserName = name;
            // The access level, plus the job role when the user has one: "admin · Maliyyəçi".
            entry.UserRole = user is null ? "system" : user.CustomRole is null ? user.Role : $"{user.Role} · {user.CustomRole}";
            entry.Description = description.Length > DescriptionMaxLength ? description[..DescriptionMaxLength] : description;
            entry.IpAddress = currentUser.IpAddress;

            db.AuditLogs.Add(entry);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The action itself is already saved; a missing log line must not turn it into an error.
            db.AuditLogs.Entry(entry).State = EntityState.Detached;
            logger.LogError(exception, "Could not write audit log for company {CompanyId}: {Module} {Action} {What}", companyId, module, action, what);
        }
    }
}
