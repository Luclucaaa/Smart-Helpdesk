using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHelpdesk.Data.Entities;

namespace SmartHelpdesk.Data.Configurations
{
    public class CommentConfiguration : IEntityTypeConfiguration<Comment>
    {
        public void Configure(EntityTypeBuilder<Comment> builder)
        {
            builder.Property(comment => comment.Text)
                    .IsRequired();
            builder.Property(comment => comment.CreatedAt)
                    .IsRequired();
            builder.HasMany(comment => comment.Attachments)
                 .WithOne(attach => attach.Comment)
                 .HasForeignKey(attach => attach.CommentId)
                 .IsRequired()
                 .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
