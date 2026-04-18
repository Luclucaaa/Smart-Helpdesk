using SmartHelpdesk.DTOs.Responses;

namespace SmartHelpdesk.Interfaces;

public interface IAiSuggestionLogService
{
    Task<List<Guid>> LogSuggestionsAsync(
        Guid ticketId,
        Guid agentId,
        IReadOnlyList<AgentReplySuggestionItem> suggestions);

    Task<bool> MarkAcceptedAsync(Guid suggestionLogId, Guid agentId);

    Task<bool> SetFeedbackAsync(Guid suggestionLogId, Guid agentId, bool isHelpful, string? note);
}
