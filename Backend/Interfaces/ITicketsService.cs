using System.Linq.Expressions;
using SmartHelpdesk.Data.Entities;
using SmartHelpdesk.Data.Enums;
using SmartHelpdesk.DTOs.Requests;
using SmartHelpdesk.DTOs.Responses;

namespace SmartHelpdesk.Interfaces
{
    public interface ITicketsService
    {
        public Task<Guid> CreateTicket(CreateTicketDTO ticketDTO);
        public Task UpdateTicket(Guid id, UpdateTicketDTO ticketDTO);
        public Task UpdateTicketStatus(Guid id, Status status);
        public Task DeleteTicket(Guid id);
        public Task<FilteredTicketsDTO> GetTickets(TicketsQueryFilters filters);
        public Task<TicketDetailsDTO> GetTicket(Guid id);
        public Task<object> GetTicketsRaw(int take, int skip, Guid? userId = null);
        public Task<object> GetAllTicketIdsForDebug();
        public Task<Ticket> GetTicketSimple(Guid id);
        public Task AssignTicket(Guid ticketId, Guid? agentId);
        public Task<object> GetAgentStats(Guid agentId);
        public Task<TicketFeedbackDTO> SubmitTicketFeedback(Guid ticketId, Guid userId, SubmitTicketFeedbackDTO dto);
        public Task<TicketFeedbackDTO?> GetTicketFeedback(Guid ticketId);
        public Task<int> ProcessSlaBreachesAsync();
        
        // 🔥 Agent Dashboard Methods
        public Task<AgentSmartQueueDTO> GetAgentSmartQueue(Guid agentId, AgentTicketFiltersDTO filters);
        public Task<AgentSmartQueueDTO> GetUnassignedTickets(AgentTicketFiltersDTO filters);
        
        // 🔥 Admin Dashboard Methods
        public Task<AdminDashboardDTO> GetAdminDashboard();
    }
}
