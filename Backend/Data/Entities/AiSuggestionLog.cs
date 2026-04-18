namespace SmartHelpdesk.Data.Entities;

public class AiSuggestionLog
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid AgentId { get; set; }
    public string SuggestionText { get; set; } = string.Empty;
    public string Source { get; set; } = "fallback";
    public float Confidence { get; set; }
    public bool IsAccepted { get; set; }
    public bool? IsHelpful { get; set; }
    public string? FeedbackNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? FeedbackAt { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public User Agent { get; set; } = null!;
}
