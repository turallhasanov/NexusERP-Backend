using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusERP.Domain.Entities;

namespace NexusERP.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Amount).HasPrecision(18, 2);
        builder.Property(t => t.Description).HasMaxLength(500);

        // The transaction list is read per company, newest first.
        builder.HasIndex(t => new { t.CompanyId, t.TransactionDate });

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.CashRegister)
            .WithMany()
            .HasForeignKey(t => t.CashRegisterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.TargetCashRegister)
            .WithMany()
            .HasForeignKey(t => t.TargetCashRegisterId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Sale)
            .WithMany()
            .HasForeignKey(t => t.SaleId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Purchase)
            .WithMany()
            .HasForeignKey(t => t.PurchaseId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
