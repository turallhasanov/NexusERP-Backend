using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusERP.Domain.Entities;

namespace NexusERP.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FullName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Title).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Department).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Salary).HasPrecision(18, 2);
        builder.Property(e => e.VacationNote).HasMaxLength(500);
        builder.Property(e => e.TerminationReason).HasMaxLength(500);
        builder.Property(e => e.TerminationOrderNumber).HasMaxLength(20);

        builder.HasIndex(e => new { e.CompanyId, e.TerminationOrderNumber })
            .IsUnique()
            .HasFilter("[TerminationOrderNumber] IS NOT NULL");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Store)
            .WithMany()
            .HasForeignKey(e => e.StoreId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
