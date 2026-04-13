namespace SmartHelpdesk.Data.Entities
{
    public class ProductAgentAssignment
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Guid AgentId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public Product Product { get; set; } = null!;
        public User Agent { get; set; } = null!;
    }
}
