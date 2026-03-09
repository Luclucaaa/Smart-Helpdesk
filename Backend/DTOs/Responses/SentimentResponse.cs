namespace SmartHelpdesk.DTOs.Responses;

/// <summary>
/// Response từ API phân tích cảm xúc
/// </summary>
public class SentimentResponse
{
    /// <summary>
    /// ID của ticket (nếu có trong request)
    /// </summary>
    public Guid? TicketId { get; set; }
    
    /// <summary>
    /// Kết quả cảm xúc: "positive", "negative", "neutral"
    /// </summary>
    public string Sentiment { get; set; } = string.Empty;
    
    /// <summary>
    /// Điểm số confidence (0.0 - 1.0)
    /// </summary>
    public float Score { get; set; }
    
    /// <summary>
    /// Chi tiết điểm số cho từng label
    /// </summary>
    public Dictionary<string, float> AllScores { get; set; } = new();
}
