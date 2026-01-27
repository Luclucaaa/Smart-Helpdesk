using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartHelpdesk.Data.Configurations;
using SmartHelpdesk.Data.Entities;

namespace SmartHelpdesk.Data
{
    public class SmartHelpdeskContext : IdentityDbContext<User, Role, Guid>
    {
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public SmartHelpdeskContext(DbContextOptions<SmartHelpdeskContext> opt)
            : base(opt) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new TicketConfiguration());
        }
    }
}
