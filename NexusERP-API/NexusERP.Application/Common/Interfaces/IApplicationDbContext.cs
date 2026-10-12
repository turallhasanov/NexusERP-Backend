using Microsoft.EntityFrameworkCore;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Company> Companies { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Store> Stores { get; }
    DbSet<Customer> Customers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
