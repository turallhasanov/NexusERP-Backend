using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusERP.Domain.Entities;

namespace NexusERP.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).IsRequired().HasMaxLength(150);
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Name).IsRequired().HasMaxLength(150);
        builder.Property(u => u.Role).IsRequired().HasMaxLength(50).HasDefaultValue("admin");

        // Identity hashes are ~84 characters; room for future formats.
        builder.Property(u => u.PasswordHash).HasMaxLength(500);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(u => u.CompanyId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
