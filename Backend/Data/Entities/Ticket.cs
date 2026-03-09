using SmartHelpdesk.Data.Enums;

namespace SmartHelpdesk.Data.Entities
{
    public class Ticket
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public Priority Priority { get; set; }
        public Status Status { get; set; }
        public Category? Category { get; set; }  // Do AI quyết định
        public float? SentimentScore { get; set; }  // Điểm cảm xúc AI chấm (0.0 - 1.0)
        public string? SentimentLabel { get; set; }  // Nhãn cảm xúc: "positive", "negative", "neutral"
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public DateTimeOffset? ClosedAt { get; set; }
        
        // Foreign Keys
        public Guid UserId { get; set; }
        public Guid? AssignedToId { get; set; }
        public Guid? ProductId { get; set; }  // Liên kết với sản phẩm (tùy chọn)
        public string? ProductName { get; set; }  // Tên sản phẩm do khách hàng nhập (tùy chọn)
        
        // Navigation
        public User User { get; set; } = null!;
        public User? AssignedTo { get; set; }
        public Product? Product { get; set; }
        public List<Comment> Comments { get; set; } = new List<Comment>();
    }
}
