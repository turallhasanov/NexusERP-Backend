using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusERP.Domain.Entities;

namespace NexusERP.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Voen).HasMaxLength(50);
        builder.Property(c => c.InviteCode).IsRequired().HasMaxLength(8);

        builder.HasIndex(c => c.InviteCode).IsUnique();
    }
}
