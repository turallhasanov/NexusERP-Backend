using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusERP.Domain.Entities;

namespace NexusERP.Infrastructure.Persistence.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Note).HasMaxLength(500);

        // The expense list and the P&L report read per company by date.
        builder.HasIndex(e => new { e.CompanyId, e.ExpenseDate });

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ExpenseCategory)
            .WithMany()
            .HasForeignKey(e => e.ExpenseCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CashRegister)
            .WithMany()
            .HasForeignKey(e => e.CashRegisterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
