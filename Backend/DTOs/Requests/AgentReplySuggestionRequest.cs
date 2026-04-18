namespace SmartHelpdesk.DTOs.Requests;

/// <summary>
/// Request body cho API goi y phan hoi cho Agent
/// </summary>
public class AgentReplySuggestionRequest
{
    public Guid TicketId { get; set; }

    /// <summary>
    /// Ban nhap nhap dang soan (optional)
    /// </summary>
    public string? DraftReply { get; set; }

    /// <summary>
    /// So goi y toi da tra ve (1-5)
    /// </summary>
    public int MaxSuggestions { get; set; } = 3;
}
