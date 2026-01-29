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
    }
}
