using SmartHelpdesk.Data.Enums;

namespace SmartHelpdesk.DTOs.Requests
{
    /// <summary>
    /// Filter cho Agent Dashboard - Smart Queue
    /// </summary>
    public class AgentTicketFiltersDTO
    {
        public int Take { get; set; } = 20;
        public int Skip { get; set; } = 0;
        public string SortColumn { get; set; } = "Priority";  // Mặc định sắp xếp theo Priority
        public string Order { get; set; } = "desc";  // desc = Priority High trước
        
        // Filters
        public Priority? Priority { get; set; }
        public Status? Status { get; set; }
        public Guid? ProductId { get; set; }  // Lọc theo sản phẩm
        public Category? Category { get; set; }  // Lọc theo Category (Bug/Feature/Sale)
        public string? SentimentLabel { get; set; }  // "positive", "negative", "neutral"
        public bool? OnlyUnassigned { get; set; } = false;  // Chỉ show unassigned tickets
    }
}
