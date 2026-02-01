using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using SmartHelpdesk.Common.Exceptions;
using SmartHelpdesk.Data;
using SmartHelpdesk.Data.Entities;
using SmartHelpdesk.Data.Enums;
using SmartHelpdesk.DTOs.Requests;
using SmartHelpdesk.DTOs.Responses;
using SmartHelpdesk.Interfaces;

namespace SmartHelpdesk.Services
{
    public class TicketsService : ITicketsService
    {
        private readonly SmartHelpdeskContext _context;
        private readonly IMapper _mapper;
        public TicketsService(SmartHelpdeskContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        public async Task<Guid> CreateTicket(CreateTicketDTO ticketDTO)
        {
            var newTicket = _mapper.Map<CreateTicketDTO, Ticket>(ticketDTO);
            newTicket.CreatedAt = DateTimeOffset.Now;
            newTicket.Status = Status.Open;
            
            // Tự động tạo Title nếu không có
            if (string.IsNullOrWhiteSpace(newTicket.Title))
            {
                // Lấy 50 ký tự đầu của Description làm Title
                newTicket.Title = newTicket.Description.Length > 50 
                    ? newTicket.Description.Substring(0, 50) + "..." 
                    : newTicket.Description;
            }

            _context.Tickets.Add(newTicket);
            await _context.SaveChangesAsync();

            return newTicket.Id;
        }

        public async Task DeleteTicket(Guid id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null)
            {
                throw new NotFoundException();
            }
            _context.Tickets.Remove(ticket);
            await _context.SaveChangesAsync();
        }

        public async Task<TicketDetailsDTO> GetTicket(Guid id)
        {
            var tickets = await _context.Tickets
                .Include(ticket => ticket.User)
                .Include(ticket => ticket.AssignedTo)
                .Include(ticket => ticket.Product)
                .Include(ticket => ticket.Comments)
                    .ThenInclude(comment => comment.User)
                .ToListAsync();
            var ticket = tickets.FirstOrDefault(t => t.Id == id);
            if (ticket == null)
            {
                throw new NotFoundException();
            }

            var ticketDto = _mapper.Map<Ticket, TicketDetailsDTO>(ticket);

            return ticketDto;
        }

        private async Task<FilteredTicketsDTO> ApplyFilters(TicketsQueryFilters filters)
        {
            Console.WriteLine($"DEBUG ApplyFilters START: UserId = {filters.UserId}");
            
            var query = _context.Tickets
                .Include(ticket => ticket.User)
                .Include(ticket => ticket.AssignedTo)
                .Include(ticket => ticket.Product)
                .Include(ticket => ticket.Comments)
                .AsQueryable();

            // Áp dụng filter UserId TRƯỚC
            if (filters.UserId != null)
            {
                Console.WriteLine($"DEBUG: Filtering by UserId = {filters.UserId}");
                query = query.Where(t => t.UserId == filters.UserId);
            }

            if (!string.IsNullOrWhiteSpace(filters.SortColumn) && !string.IsNullOrWhiteSpace(filters.Order))
            {
                if (filters.Order == "asc")
                {
                    query = query.OrderBy(e => EF.Property<object>(e, filters.SortColumn));
                }
                else if (filters.Order == "desc")
                {
                    query = query.OrderByDescending(e => EF.Property<object>(e, filters.SortColumn));
                }
            }

            if(filters.Priority != null)
            {
                query = query.Where(t => t.Priority == filters.Priority);
            }

            if (filters.Status != null)
            {
                query = query.Where(t => t.Status == filters.Status);
            }

            if (filters.AsignedToId != null)
            {
                query = query.Where(t => t.AssignedToId == filters.AsignedToId);
            }

            // Đếm TOTAL sau khi đã áp dụng tất cả filter
            var total = await query.CountAsync();
            var take = filters.Take;
            var skip = filters.Skip;

            Console.WriteLine($"DEBUG Service: Total after filter = {total}, Take = {take}, Skip = {skip}");

            var tickets = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            Console.WriteLine($"DEBUG Service: Fetched {tickets.Count} tickets");
            foreach (var t in tickets)
            {
                Console.WriteLine($"DEBUG Service: Ticket {t.Id} - User {t.UserId}");
            }

            var ticketsDTOs = _mapper.Map<List<Ticket>, List<TicketDTO>>(tickets);

            Console.WriteLine($"DEBUG Service: Mapped to {ticketsDTOs.Count} DTOs");


            var filteredTickets = new FilteredTicketsDTO
            {
                Take = take,
                Skip = skip,
                Total = total,
                Tickets = ticketsDTOs
            };

            return filteredTickets;
        } 

        public async Task<FilteredTicketsDTO> GetTickets(TicketsQueryFilters filters)
        {
            var filteredTickets = await ApplyFilters(filters);

            return filteredTickets;
        }

        public async Task<object> GetTicketsRaw(int take, int skip)
        {
            Console.WriteLine($"DEBUG GetTicketsRaw: take={take}, skip={skip}");
            
            var total = await _context.Tickets.CountAsync();
            Console.WriteLine($"DEBUG GetTicketsRaw: total count = {total}");
            
            var tickets = await _context.Tickets
                .Include(t => t.User)
                .OrderByDescending(t => t.CreatedAt)
                .Skip(skip)
                .Take(take)
                .Select(t => new 
                {
                    t.Id,
                    t.Title,
                    t.Description,
                    Status = (int)t.Status,
                    Priority = (int)t.Priority,
                    Category = t.Category != null ? (int?)t.Category : null,
                    t.SentimentScore,
                    t.ProductName,
                    t.CreatedAt,
                    t.UserId,
                    UserName = t.User != null ? t.User.Name : "",
                    UserEmail = t.User != null ? t.User.Email : ""
                })
                .ToListAsync();
            
            Console.WriteLine($"DEBUG GetTicketsRaw: fetched {tickets.Count} tickets");
            
            return new 
            {
                take,
                skip,
                total,
                tickets
            };
        }

        public async Task UpdateTicket(Guid id, UpdateTicketDTO ticketDTO)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null)
            {
                throw new NotFoundException();
            }

            if (ticket.Status == Status.Closed)
            {
                throw new ForbiddenException();
            }

            ticket.Title = ticketDTO.Title;
            ticket.Description = ticketDTO.Description;
            ticket.Priority = ticketDTO.Priority;
            ticket.Status = ticketDTO.Status;
            ticket.UpdatedAt = DateTimeOffset.Now;
            ticket.AssignedToId = ticketDTO.AssignedToId;

            if(ticketDTO.Status == Status.Closed)
            {
                ticket.ClosedAt = DateTimeOffset.Now;
            }

            await _context.SaveChangesAsync();
        }

        public async Task UpdateTicketStatus(Guid id, Status status)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null)
            {
                throw new NotFoundException();
            }

            ticket.Status = status;
            ticket.UpdatedAt = DateTimeOffset.Now;

            if (status == Status.Closed)
            {
                ticket.ClosedAt = DateTimeOffset.Now;
            }

            await _context.SaveChangesAsync();
        }
    }
}
