using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHelpdesk.Data.Entities;

namespace SmartHelpdesk.Data.Configurations;

public class AiSuggestionLogConfiguration : IEntityTypeConfiguration<AiSuggestionLog>
{
    public void Configure(EntityTypeBuilder<AiSuggestionLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SuggestionText)
            .HasMaxLength(3000)
            .IsRequired();

        builder.Property(x => x.Source)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Confidence)
            .HasDefaultValue(0.7f);

        builder.Property(x => x.FeedbackNote)
            .HasMaxLength(500);

        builder.Property(x => x.IsAccepted)
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.AgentId, x.CreatedAt });
        builder.HasIndex(x => new { x.TicketId, x.CreatedAt });
        builder.HasIndex(x => new { x.AgentId, x.IsAccepted });

        builder.HasOne(x => x.Ticket)
            .WithMany(t => t.AiSuggestionLogs)
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Agent)
            .WithMany(u => u.AiSuggestionLogs)
            .HasForeignKey(x => x.AgentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
