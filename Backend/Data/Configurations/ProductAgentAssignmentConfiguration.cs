using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHelpdesk.Data.Entities;

namespace SmartHelpdesk.Data.Configurations
{
    public class ProductAgentAssignmentConfiguration : IEntityTypeConfiguration<ProductAgentAssignment>
    {
        public void Configure(EntityTypeBuilder<ProductAgentAssignment> builder)
        {
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => new { x.ProductId, x.AgentId })
                .IsUnique();

            builder.Property(x => x.IsActive)
                .HasDefaultValue(true);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasOne(x => x.Product)
                .WithMany(p => p.AgentAssignments)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Agent)
                .WithMany(u => u.ProductAssignments)
                .HasForeignKey(x => x.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
