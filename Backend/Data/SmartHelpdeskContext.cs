using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartHelpdesk.Data.Configurations;
using SmartHelpdesk.Data.Entities;

namespace SmartHelpdesk.Data
{
    public class SmartHelpdeskContext : IdentityDbContext<User, Role, Guid, 
        IdentityUserClaim<Guid>, IdentityUserRole<Guid>, IdentityUserLogin<Guid>, 
        IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>
    {
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<Product> Products { get; set; }
        
        public SmartHelpdeskContext(DbContextOptions<SmartHelpdeskContext> opt)
            : base(opt) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Đổi tên các bảng Identity - bỏ prefix "AspNet"
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<Role>().ToTable("Roles");
            modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
            
            // Loại bỏ các bảng không cần thiết
            modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
            modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
            modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
            modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
            
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new TicketConfiguration());
            modelBuilder.ApplyConfiguration(new ProductConfiguration());
        }
    }
}
