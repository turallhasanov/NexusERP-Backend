using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NexusERP.Infrastructure.Persistence;

namespace NexusERP.Application.Tests.TestSupport;

/// <summary>
/// A private SQLite database that lives in memory for as long as this object does, created from the real
/// <see cref="AppDbContext"/> model (same tables, keys, unique indexes and foreign keys). Contexts from
/// <see cref="CreateContext"/> share it, so a test can act through one context and check the result through a fresh one.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteTestDatabase()
    {
        _connection.Open();

        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateContext() =>
        new SqliteAppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}

/// <summary>
/// <see cref="AppDbContext"/> with decimals stored as REAL. SQLite has no decimal type and EF refuses SUM over decimal
/// columns there (EF's documented workaround is this converter); SQL Server keeps decimal(18,2)/(18,3). Every value the
/// tests use is exact in a double, and the services round in C# before saving, so results match SQL Server's.
/// </summary>
public sealed class SqliteAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            if (property.ClrType == typeof(decimal))
                property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<decimal, double>(
                    v => (double)v, v => (decimal)v));
            else if (property.ClrType == typeof(decimal?))
                property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<decimal?, double?>(
                    v => (double?)v, v => (decimal?)v));
        }
    }
}
