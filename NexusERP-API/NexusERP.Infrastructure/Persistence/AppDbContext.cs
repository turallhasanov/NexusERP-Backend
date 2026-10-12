using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;

namespace NexusERP.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
