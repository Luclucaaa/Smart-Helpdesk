using Microsoft.EntityFrameworkCore;
using SmartHelpdesk.Data;
using SmartHelpdesk.Data.Entities;
using SmartHelpdesk.DTOs.Responses;
using SmartHelpdesk.Interfaces;

namespace SmartHelpdesk.Services;

public class AiSuggestionLogService : IAiSuggestionLogService
{
    private readonly SmartHelpdeskContext _context;

    public AiSuggestionLogService(SmartHelpdeskContext context)
    {
        _context = context;
    }

    public async Task<List<Guid>> LogSuggestionsAsync(
        Guid ticketId,
        Guid agentId,
        IReadOnlyList<AgentReplySuggestionItem> suggestions)
    {
        var ids = new List<Guid>();

        foreach (var suggestion in suggestions)
        {
            var id = Guid.NewGuid();
            var entity = new AiSuggestionLog
            {
                Id = id,
                TicketId = ticketId,
                AgentId = agentId,
                SuggestionText = suggestion.Text,
                Source = string.IsNullOrWhiteSpace(suggestion.Source) ? "unknown" : suggestion.Source,
                Confidence = suggestion.Confidence,
                IsAccepted = false,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.Set<AiSuggestionLog>().Add(entity);
            ids.Add(id);
        }

        if (ids.Count > 0)
        {
            await _context.SaveChangesAsync();
        }

        return ids;
    }

    public async Task<bool> MarkAcceptedAsync(Guid suggestionLogId, Guid agentId)
    {
        var log = await _context.Set<AiSuggestionLog>()
            .FirstOrDefaultAsync(x => x.Id == suggestionLogId && x.AgentId == agentId);

        if (log == null)
        {
            return false;
        }

        if (!log.IsAccepted)
        {
            log.IsAccepted = true;
            log.AcceptedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();
        }

        return true;
    }

    public async Task<bool> SetFeedbackAsync(Guid suggestionLogId, Guid agentId, bool isHelpful, string? note)
    {
        var log = await _context.Set<AiSuggestionLog>()
            .FirstOrDefaultAsync(x => x.Id == suggestionLogId && x.AgentId == agentId);

        if (log == null)
        {
            return false;
        }

        log.IsHelpful = isHelpful;
        log.FeedbackNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        log.FeedbackAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }
}
