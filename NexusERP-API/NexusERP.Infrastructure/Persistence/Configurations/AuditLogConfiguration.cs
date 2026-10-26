using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusERP.Domain.Entities;

namespace NexusERP.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.UserRole).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Department).IsRequired().HasMaxLength(50);
        builder.Property(a => a.Description).IsRequired().HasMaxLength(1000);
        builder.Property(a => a.IpAddress).HasMaxLength(64);

        // Newest first per company, filtered by date range, module or department.
        builder.HasIndex(a => new { a.CompanyId, a.CreatedAt });
        builder.HasIndex(a => new { a.CompanyId, a.ModuleName, a.CreatedAt });

        // No foreign key to Users: an entry outlives the account it names.
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(a => a.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
