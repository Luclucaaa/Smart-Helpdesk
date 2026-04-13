using SmartHelpdesk.Data.Enums;

namespace SmartHelpdesk.DTOs.Responses
{
    /// <summary>
    /// DTO cho từng ticket trong Agent Smart Queue
    /// </summary>
    public class AgentQueueTicketDTO
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public Priority Priority { get; set; }
        public Status Status { get; set; }
        public Category? Category { get; set; }
        public string? SentimentLabel { get; set; }  // "positive", "negative", "neutral"
        public float? SentimentScore { get; set; }  // 0.0 - 1.0
        
        // Customer info
        public string CustomerName { get; set; } = null!;
        public string CustomerEmail { get; set; } = null!;
        
        // Product info
        public string? ProductName { get; set; }
        public Guid? ProductId { get; set; }
        
        // Assignment info
        public string? AssignedToName { get; set; }
        public Guid? AssignedToId { get; set; }
        
        // Timeline
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ResolutionDueAt { get; set; }
        public bool IsSlaBreached { get; set; }
        public int WaitingMinutes { get; set; }  // Tính toán: (Now - CreatedAt).TotalMinutes
        public int CommentsCount { get; set; }  // Số bình luận
        
        // Weight score cho sorting (Priority High + waiting time lâu = score cao)
        public int WeightScore { get; set; }
    }
}
