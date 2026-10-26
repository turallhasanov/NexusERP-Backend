using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusERP.Domain.Entities;

namespace NexusERP.Infrastructure.Persistence.Configurations;

public class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.HasKey(t => t.Id);

        // The desk lists by status, newest activity first; users list their own tickets.
        builder.HasIndex(t => new { t.Status, t.LastMessageAt });
        builder.HasIndex(t => new { t.CompanyId, t.UserId });

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Messages)
            .WithOne()
            .HasForeignKey(m => m.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SupportMessageConfiguration : IEntityTypeConfiguration<SupportMessage>
{
    public void Configure(EntityTypeBuilder<SupportMessage> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.MessageText).IsRequired().HasMaxLength(2000);
        builder.HasIndex(m => new { m.TicketId, m.SentAt });

        builder.HasOne(m => m.SenderUser)
            .WithMany()
            .HasForeignKey(m => m.SenderUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
