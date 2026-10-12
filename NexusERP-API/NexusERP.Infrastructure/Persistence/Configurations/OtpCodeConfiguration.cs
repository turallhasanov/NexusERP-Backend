using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusERP.Domain.Entities;

namespace NexusERP.Infrastructure.Persistence.Configurations;

public class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> builder)
    {
        builder.HasKey(o => o.Id);

        // Same length as Users.Email. Not a foreign key: a reset can be requested for an address with no
        // account, and codes must not reveal whether one exists.
        builder.Property(o => o.Email).IsRequired().HasMaxLength(150);
        builder.Property(o => o.CodeHash).IsRequired().HasMaxLength(64);

        // Lookups are always "the latest code for this address and purpose".
        builder.HasIndex(o => new { o.Email, o.Purpose, o.CreatedAt });
    }
}
