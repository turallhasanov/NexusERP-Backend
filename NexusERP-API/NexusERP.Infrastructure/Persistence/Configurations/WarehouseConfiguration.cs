using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusERP.Domain.Entities;

namespace NexusERP.Infrastructure.Persistence.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Code).IsRequired().HasMaxLength(20);
        builder.Property(w => w.Name).IsRequired().HasMaxLength(200);
        builder.Property(w => w.Address).HasMaxLength(500);

        builder.HasIndex(w => new { w.CompanyId, w.Code }).IsUnique();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(w => w.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
