using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusERP.Domain.Entities;

namespace NexusERP.Infrastructure.Persistence.Configurations;

public class SupportTicketViewConfiguration : IEntityTypeConfiguration<SupportTicketView>
{
    public void Configure(EntityTypeBuilder<SupportTicketView> builder)
    {
        builder.HasKey(v => new { v.TicketId, v.UserId });

        builder.HasOne<SupportTicket>()
            .WithMany()
            .HasForeignKey(v => v.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
