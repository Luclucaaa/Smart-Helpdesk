using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHelpdesk.Data.Entities;

namespace SmartHelpdesk.Data.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.HasKey(p => p.Id);
            
            builder.Property(p => p.Name)
                .HasMaxLength(200)
                .IsRequired();
                
            builder.Property(p => p.Description)
                .HasMaxLength(1000);
                
            builder.HasMany(p => p.Tickets)
                .WithOne(t => t.Product)
                .HasForeignKey(t => t.ProductId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
