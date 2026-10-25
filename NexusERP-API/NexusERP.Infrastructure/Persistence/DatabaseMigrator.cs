using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NexusERP.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    /// <summary>
    /// Brings the database schema up to date before anything (the seeders, the first request) queries it;
    /// otherwise a column added in the model but not yet in the database fails with SQL error 207.
    /// With <paramref name="applyPending"/> (Development) pending migrations are applied; elsewhere startup
    /// stops instead, since migrating a production database is a deliberate deployment step.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, bool applyPending, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseMigrator));

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        if (pending.Count == 0)
            return;

        if (!applyPending)
            throw new InvalidOperationException(
                $"The database has {pending.Count} pending migration(s): {string.Join(", ", pending)}. " +
                "Apply them (dotnet ef database update) before starting the API.");

        logger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync(cancellationToken);
    }
}
