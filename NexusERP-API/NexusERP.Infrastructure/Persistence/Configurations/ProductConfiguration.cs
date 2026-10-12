using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusERP.Domain.Entities;

namespace NexusERP.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code).IsRequired().HasMaxLength(20);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Barcode).IsRequired().HasMaxLength(50);
        builder.Property(p => p.PurchasePrice).HasPrecision(18, 2);
        builder.Property(p => p.SalePrice).HasPrecision(18, 2);

        builder.HasIndex(p => new { p.CompanyId, p.Code }).IsUnique();
        builder.HasIndex(p => new { p.CompanyId, p.Barcode }).IsUnique();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(p => p.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Unit)
            .WithMany()
            .HasForeignKey(p => p.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
