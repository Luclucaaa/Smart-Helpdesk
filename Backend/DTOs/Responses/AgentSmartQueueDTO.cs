namespace SmartHelpdesk.DTOs.Responses
{
    /// <summary>
    /// Response cho Agent Smart Queue (danh sách + stats)
    /// </summary>
    public class AgentSmartQueueDTO
    {
        public List<AgentQueueTicketDTO> Tickets { get; set; } = new();
        public int Total { get; set; }  // Tổng số tickets (không có phân trang)
        public int Take { get; set; }
        public int Skip { get; set; }
        
        // Statistics
        public int HighPriorityCount { get; set; }  // Tickets với Priority = High
        public int NegativeSentimentCount { get; set; }  // Tickets với sentiment = negative
        public int UnassignedCount { get; set; }  // Tickets chưa được assign
    }
}
