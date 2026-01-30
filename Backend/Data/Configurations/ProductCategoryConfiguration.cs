using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHelpdesk.Data.Entities;

namespace SmartHelpdesk.Data.Configurations
{
    public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
    {
        public void Configure(EntityTypeBuilder<ProductCategory> builder)
        {
            builder.ToTable("ProductCategories");
            
            builder.HasKey(x => x.Id);
            
            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);
                
            builder.Property(x => x.Description)
                .HasMaxLength(500);
                
            builder.Property(x => x.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
                
            // Relationship
            builder.HasMany(c => c.Products)
                .WithOne(p => p.Category)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
