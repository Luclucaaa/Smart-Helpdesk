namespace SmartHelpdesk.Data.Entities
{
    public class ProductCategory
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        
        // Navigation
        public List<Product> Products { get; set; } = new();
    }
}
