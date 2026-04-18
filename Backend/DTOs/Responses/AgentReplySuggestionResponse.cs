namespace SmartHelpdesk.DTOs.Responses;

public class AgentReplySuggestionResponse
{
    public Guid TicketId { get; set; }
    public string Source { get; set; } = "fallback";
    public List<AgentReplySuggestionItem> Suggestions { get; set; } = new();
}

public class AgentReplySuggestionItem
{
    public Guid SuggestionLogId { get; set; }
    public string Text { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public string Source { get; set; } = "fallback";
}
