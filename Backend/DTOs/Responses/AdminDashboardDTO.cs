namespace SmartHelpdesk.DTOs.Responses
{
    /// <summary>
    /// Dashboard chính cho Admin - Tổng hợp metrics
    /// </summary>
    public class AdminDashboardDTO
    {
        // General Stats
        public int TotalTickets { get; set; }
        public int OpenTickets { get; set; }
        public int InProgressTickets { get; set; }
        public int ClosedTickets { get; set; }
        
        // Sentiment Stats
        public int PositiveSentimentCount { get; set; }
        public int NegativeSentimentCount { get; set; }
        public int NeutralSentimentCount { get; set; }
        public float AverageSentimentScore { get; set; }  // 0.0 - 1.0
        
        // Priority Stats
        public int HighPriorityCount { get; set; }
        public int MediumPriorityCount { get; set; }
        public int LowPriorityCount { get; set; }
        
        // Category Stats
        public int BugCount { get; set; }
        public int FeatureCount { get; set; }
        public int SupportCount { get; set; }
        public int SaleCount { get; set; }
        
        // Product Stats
        public List<ProductStatDTO> ProductStats { get; set; } = new();
        
        // Agent Stats
        public List<AgentPerformanceDTO> AgentStats { get; set; } = new();
        
        // Time-based stats
        public List<TicketTrendDTO> TicketTrends { get; set; } = new();  // Last 30 days

        // SLA + CSAT
        public int SlaBreachedTickets { get; set; }
        public int FeedbackCount { get; set; }
        public float AverageCsatRating { get; set; }
    }

    public class ProductStatDTO
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public int TotalTickets { get; set; }
        public int OpenTickets { get; set; }
        public float PositiveSentimentPercentage { get; set; }
        public float NegativeSentimentPercentage { get; set; }
    }

    public class AgentPerformanceDTO
    {
        public Guid AgentId { get; set; }
        public string AgentName { get; set; } = null!;
        public int AssignedTickets { get; set; }
        public int ClosedTickets { get; set; }
        public int OpenTickets { get; set; }
        public float AverageResolutionTimeHours { get; set; }
        public float CustomerSatisfactionPercentage { get; set; }  // Dựa trên sentiment
        public float AverageCsatRating { get; set; }
    }

    public class TicketTrendDTO
    {
        public DateTime Date { get; set; }
        public int NewTickets { get; set; }
        public int ClosedTickets { get; set; }
        public int TotalOpen { get; set; }
    }
}
