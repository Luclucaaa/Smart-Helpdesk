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
        private readonly ISentimentService _sentimentService;
        private readonly ILogger<TicketsService> _logger;

        public TicketsService(
            SmartHelpdeskContext context, 
            IMapper mapper,
            ISentimentService sentimentService,
            ILogger<TicketsService> logger)
        {
            _context = context;
            _mapper = mapper;
            _sentimentService = sentimentService;
            _logger = logger;
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

            // 🔥 AI: Phân tích cảm xúc và tự động set Priority
            try
            {
                var sentimentResult = _sentimentService.AnalyzeSentiment(newTicket.Description);
                
                newTicket.SentimentScore = sentimentResult.Score;
                newTicket.SentimentLabel = sentimentResult.Sentiment;
                
                // Tự động set Priority dựa trên sentiment
                // Negative với score cao -> High priority
                // Positive hoặc Neutral -> Giữ nguyên mức mặc định
                if (sentimentResult.Sentiment == "negative" && sentimentResult.Score > 0.6f)
                {
                    newTicket.Priority = Priority.High;
                    _logger.LogInformation(
                        "Ticket auto-prioritized to HIGH due to negative sentiment. Score: {Score}", 
                        sentimentResult.Score);
                }
                else if (sentimentResult.Sentiment == "negative")
                {
                    newTicket.Priority = Priority.Medium;
                }
                else
                {
                    // Giữ priority mặc định (Low) cho positive/neutral
                    if (newTicket.Priority == default)
                    {
                        newTicket.Priority = Priority.Low;
                    }
                }
                
                _logger.LogInformation(
                    "Sentiment analyzed for new ticket: {Sentiment} (Score: {Score:F2}), Priority: {Priority}",
                    sentimentResult.Sentiment, 
                    sentimentResult.Score,
                    newTicket.Priority);
            }
            catch (Exception ex)
            {
                // Nếu AI fail, vẫn tiếp tục tạo ticket với priority mặc định
                _logger.LogWarning(ex, "Failed to analyze sentiment, using default priority");
                if (newTicket.Priority == default)
                {
                    newTicket.Priority = Priority.Low;
                }
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
            // Query ticket with Select to avoid nullable Guid issues
            var ticketData = await _context.Tickets
                .Where(t => t.Id == id)
                .Select(t => new 
                {
                    t.Id,
                    t.Title,
                    t.Description,
                    t.Priority,
                    t.Status,
                    t.Category,
                    t.SentimentScore,
                    t.SentimentLabel,
                    t.CreatedAt,
                    t.UpdatedAt,
                    t.ClosedAt,
                    t.UserId,
                    t.ProductName
                })
                .FirstOrDefaultAsync();
                
            if (ticketData == null)
            {
                throw new NotFoundException();
            }
            
            // Load User separately
            var user = await _context.Users.FindAsync(ticketData.UserId);
            
            // Load comments separately
            var comments = await _context.Comments
                .Where(c => c.TicketId == id)
                .Select(c => new 
                {
                    c.Id,
                    c.Text,
                    c.CreatedAt,
                    c.UpdatedAt,
                    c.UserId,
                    c.TicketId
                })
                .ToListAsync();
            
            // Load comment users
            var commentUserIds = comments.Select(c => c.UserId).Distinct().ToList();
            var commentUsers = await _context.Users
                .Where(u => commentUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);
            
            // Build DTO manually
            var ticketDto = new TicketDetailsDTO
            {
                Id = ticketData.Id,
                Title = ticketData.Title,
                Description = ticketData.Description,
                Priority = ticketData.Priority,
                Status = ticketData.Status,
                Category = ticketData.Category,
                SentimentScore = ticketData.SentimentScore,
                SentimentLabel = ticketData.SentimentLabel,
                CreatedAt = ticketData.CreatedAt,
                UpdatedAt = ticketData.UpdatedAt,
                ClosedAt = ticketData.ClosedAt,
                UserId = ticketData.UserId,
                UserName = user?.Name ?? "",
                UserEmail = user?.Email ?? "",
                ProductName = ticketData.ProductName,
                Comments = comments.Select(c => new CommentDTO
                {
                    Id = c.Id,
                    Text = c.Text,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    UserId = c.UserId,
                    UserName = commentUsers.ContainsKey(c.UserId) ? commentUsers[c.UserId].Name : "",
                    UserEmail = commentUsers.ContainsKey(c.UserId) ? commentUsers[c.UserId].Email : "",
                    IsFromAgent = c.UserId != ticketData.UserId,
                    TicketId = c.TicketId,
                    TicketTitle = ticketData.Title
                }).ToList()
            };

            return ticketDto;
        }

        public async Task<object> GetAllTicketIdsForDebug()
        {
            var tickets = await _context.Tickets
                .Select(t => new { t.Id, t.UserId, t.Description })
                .ToListAsync();
            
            return new 
            {
                TotalCount = tickets.Count,
                Tickets = tickets.Select(t => new 
                {
                    Id = t.Id.ToString(),
                    UserId = t.UserId.ToString(),
                    Description = t.Description?.Substring(0, Math.Min(30, t.Description?.Length ?? 0))
                })
            };
        }

        public async Task<Ticket> GetTicketSimple(Guid id)
        {
            // Minimal query - only non-nullable fields
            var ticketData = await _context.Tickets
                .Where(t => t.Id == id)
                .Select(t => new 
                {
                    t.Id,
                    t.Title,
                    t.Description,
                    t.UserId
                })
                .FirstOrDefaultAsync();
                
            if (ticketData == null)
            {
                throw new NotFoundException();
            }
            
            // Return minimal ticket
            var ticket = new Ticket
            {
                Id = ticketData.Id,
                Title = ticketData.Title,
                Description = ticketData.Description,
                UserId = ticketData.UserId
            };
            
            return ticket;
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

        public async Task<object> GetTicketsRaw(int take, int skip, Guid? userId = null)
        {
            Console.WriteLine($"DEBUG GetTicketsRaw: take={take}, skip={skip}, userId={userId}");
            
            var query = _context.Tickets.AsQueryable();
            
            // Filter by userId if provided (for customer's "My Tickets")
            if (userId.HasValue)
            {
                query = query.Where(t => t.UserId == userId.Value);
                Console.WriteLine($"DEBUG GetTicketsRaw: filtering by userId = {userId}");
            }
            
            var total = await query.CountAsync();
            Console.WriteLine($"DEBUG GetTicketsRaw: total count = {total}");
            
            var tickets = await query
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
