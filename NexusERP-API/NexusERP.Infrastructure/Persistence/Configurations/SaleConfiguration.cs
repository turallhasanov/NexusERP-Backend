using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusERP.Domain.Entities;

namespace NexusERP.Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.DocumentNumber).IsRequired().HasMaxLength(20);
        builder.Property(s => s.TotalAmount).HasPrecision(18, 2);

        builder.HasIndex(s => new { s.CompanyId, s.DocumentNumber }).IsUnique();

        builder.Property(s => s.ExternalOrderNumber).HasMaxLength(50);
        builder.Property(s => s.RetailCustomerName).HasMaxLength(200);
        builder.Property(s => s.RetailCustomerPhone).HasMaxLength(50);
        builder.Property(s => s.RetailCustomerEmail).HasMaxLength(200);
        builder.Property(s => s.RetailCustomerAddress).HasMaxLength(500);

        builder.HasIndex(s => new { s.CompanyId, s.ExternalOrderNumber })
            .IsUnique()
            .HasFilter("[ExternalOrderNumber] IS NOT NULL");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(s => s.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Store)
            .WithMany()
            .HasForeignKey(s => s.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Warehouse)
            .WithMany()
            .HasForeignKey(s => s.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Customer)
            .WithMany()
            .HasForeignKey(s => s.CustomerId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey(i => i.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
