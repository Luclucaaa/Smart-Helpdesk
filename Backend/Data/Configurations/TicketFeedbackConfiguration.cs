using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHelpdesk.Data.Entities;

namespace SmartHelpdesk.Data.Configurations
{
    public class TicketFeedbackConfiguration : IEntityTypeConfiguration<TicketFeedback>
    {
        public void Configure(EntityTypeBuilder<TicketFeedback> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Rating)
                .IsRequired();

            builder.Property(x => x.Comment)
                .HasMaxLength(1000);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasIndex(x => x.TicketId)
                .IsUnique();

            builder.HasOne(x => x.Ticket)
                .WithOne(t => t.Feedback)
                .HasForeignKey<TicketFeedback>(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.User)
                .WithMany(u => u.TicketFeedbacks)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
