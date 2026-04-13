using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHelpdesk.Data.Entities;

namespace SmartHelpdesk.Data.Configurations
{
    public class CannedResponseConfiguration : IEntityTypeConfiguration<CannedResponse>
    {
        public void Configure(EntityTypeBuilder<CannedResponse> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Title)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(c => c.Text)
                .IsRequired()
                .HasMaxLength(3000);

            builder.Property(c => c.CreatedAt)
                .IsRequired();

            builder.Property(c => c.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Relationships
            builder.HasOne(c => c.CreatedBy)
                .WithMany()
                .HasForeignKey(c => c.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            builder.HasIndex(c => c.IsActive);
            builder.HasIndex(c => c.Category);
        }
    }
}
