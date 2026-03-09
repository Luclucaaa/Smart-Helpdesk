namespace SmartHelpdesk.DTOs.Requests;

/// <summary>
/// Request body cho API phân tích cảm xúc
/// </summary>
public class SentimentRequest
{
    /// <summary>
    /// ID của ticket (optional, để tracking)
    /// </summary>
    public Guid? TicketId { get; set; }
    
    /// <summary>
    /// Văn bản cần phân tích cảm xúc
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
