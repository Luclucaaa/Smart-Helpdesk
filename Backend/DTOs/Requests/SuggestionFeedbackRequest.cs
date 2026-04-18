namespace SmartHelpdesk.DTOs.Requests;

public class SuggestionFeedbackRequest
{
    public bool IsHelpful { get; set; }
    public string? Note { get; set; }
}
