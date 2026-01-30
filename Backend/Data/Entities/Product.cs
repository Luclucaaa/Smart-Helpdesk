namespace SmartHelpdesk.Data.Entities
{
    public class Product
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Foreign Key
        public Guid? CategoryId { get; set; }
        
        // Navigation
        public ProductCategory? Category { get; set; }
        public List<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}
