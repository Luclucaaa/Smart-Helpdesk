using AutoMapper;
using Microsoft.EntityFrameworkCore;
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
        private readonly ICategoryClassifierService _categoryClassifierService;
        private readonly ILogger<TicketsService> _logger;

        public TicketsService(
            SmartHelpdeskContext context, 
            IMapper mapper,
            ISentimentService sentimentService,
            ICategoryClassifierService categoryClassifierService,
            ILogger<TicketsService> logger)
        {
            _context = context;
            _mapper = mapper;
            _sentimentService = sentimentService;
            _categoryClassifierService = categoryClassifierService;
            _logger = logger;
        }

        private static DateTimeOffset CalculateResolutionDueAt(Priority priority, DateTimeOffset createdAt)
        {
            return priority switch
            {
                Priority.High => createdAt.AddHours(4),
                Priority.Medium => createdAt.AddHours(12),
                _ => createdAt.AddHours(24)
            };
        }

        private async Task<Guid?> GetBestAgentForProductAsync(Guid productId)
        {
            var assignedAgentIds = await _context.ProductAgentAssignments
                .Where(x => x.ProductId == productId && x.IsActive)
                .Select(x => x.AgentId)
                .Distinct()
                .ToListAsync();

            if (assignedAgentIds.Count == 0)
                return null;

            var workload = await _context.Tickets
                .Where(t => t.AssignedToId.HasValue && assignedAgentIds.Contains(t.AssignedToId.Value) && t.Status != Status.Closed)
                .GroupBy(t => t.AssignedToId!.Value)
                .Select(g => new { AgentId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.AgentId, x => x.Count);

            var best = assignedAgentIds
                .Select(agentId => new { AgentId = agentId, Count = workload.TryGetValue(agentId, out var count) ? count : 0 })
                .OrderBy(x => x.Count)
                .ThenBy(x => x.AgentId)
                .FirstOrDefault();

            return best?.AgentId;
        }

        private async Task CreateNotificationAsync(Guid userId, Guid? ticketId, string type, string title, string message)
        {
            var notification = new UserNotification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TicketId = ticketId,
                Type = type,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.UserNotifications.Add(notification);
            await _context.SaveChangesAsync();
        }
        public async Task<Guid> CreateTicket(CreateTicketDTO ticketDTO)
        {
            var newTicket = _mapper.Map<CreateTicketDTO, Ticket>(ticketDTO);
            newTicket.CreatedAt = DateTimeOffset.UtcNow;
            newTicket.Status = Status.Open;
            newTicket.IsSlaBreached = false;
            
            // Tự động tạo Title nếu không có
            if (string.IsNullOrWhiteSpace(newTicket.Title))
            {
                // Lấy 50 ký tự đầu của Description làm Title
                newTicket.Title = newTicket.Description.Length > 50 
                    ? newTicket.Description.Substring(0, 50) + "..." 
                    : newTicket.Description;
            }

            // AI classify category from title + description + product context
            var categoryResult = _categoryClassifierService.Classify(
                newTicket.Description,
                newTicket.Title,
                newTicket.ProductName);
            newTicket.Category = categoryResult.Category;

            // 🔥 AI: Phân tích cảm xúc và tự động set Priority
            try
            {
                var sentimentResult = _sentimentService.AnalyzeSentiment(newTicket.Description);
                
                newTicket.SentimentScore = sentimentResult.Score;
                newTicket.SentimentLabel = sentimentResult.Sentiment;
                
                // Tự động set Priority dựa trên sentiment
                // positive -> Thấp (1), neutral -> Trung bình (2), negative -> Cao (3)
                newTicket.Priority = sentimentResult.Sentiment switch
                {
                    "negative" => Priority.High,      // 3
                    "neutral"  => Priority.Medium,     // 2
                    _          => Priority.Low,        // 1 (positive hoặc unknown)
                };
                
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
                newTicket.Priority = Priority.Low;
            }

            newTicket.ResolutionDueAt = CalculateResolutionDueAt(newTicket.Priority, newTicket.CreatedAt);

            // Auto-route by product ownership when customer does not choose assignee.
            if (!newTicket.AssignedToId.HasValue && newTicket.ProductId.HasValue)
            {
                var bestAgentId = await GetBestAgentForProductAsync(newTicket.ProductId.Value);
                if (bestAgentId.HasValue)
                {
                    newTicket.AssignedToId = bestAgentId.Value;
                    newTicket.Status = Status.InProgress;
                }
            }

            _context.Tickets.Add(newTicket);
            await _context.SaveChangesAsync();

            if (newTicket.AssignedToId.HasValue)
            {
                await CreateNotificationAsync(
                    newTicket.AssignedToId.Value,
                    newTicket.Id,
                    "ticket_assigned",
                    "Ticket moi da duoc giao",
                    $"Ticket #{newTicket.Id.ToString()[..8].ToUpper()} da duoc tu dong giao cho ban.");
            }

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
                    t.FirstResponseAt,
                    t.ResolutionDueAt,
                    t.IsSlaBreached,
                    t.UserId,
                    t.AssignedToId,
                    t.ProductName
                })
                .FirstOrDefaultAsync();
                
            if (ticketData == null)
            {
                throw new NotFoundException();
            }
            
            // Load User separately
            var user = await _context.Users.FindAsync(ticketData.UserId);
            
            // Load AssignedTo separately
            User? assignedToUser = null;
            if (ticketData.AssignedToId.HasValue)
            {
                assignedToUser = await _context.Users.FindAsync(ticketData.AssignedToId.Value);
            }
            
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
                FirstResponseAt = ticketData.FirstResponseAt,
                ResolutionDueAt = ticketData.ResolutionDueAt,
                IsSlaBreached = ticketData.IsSlaBreached,
                UserId = ticketData.UserId,
                UserName = user?.Name ?? "",
                UserEmail = user?.Email ?? "",
                AssignedToName = assignedToUser != null ? $"{(assignedToUser.Name ?? "").Trim()} {(assignedToUser.Surname ?? "").Trim()}".Trim() : null,
                AssignedToEmail = assignedToUser?.Email,
                AssignedToId = ticketData.AssignedToId,
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

            var feedback = await _context.TicketFeedbacks
                .Where(x => x.TicketId == id)
                .Select(x => new { x.Rating, x.Comment })
                .FirstOrDefaultAsync();

            if (feedback != null)
            {
                ticketDto.FeedbackRating = feedback.Rating;
                ticketDto.FeedbackComment = feedback.Comment;
            }


            // Aggregate attachments from comment attachments (Attachment entity is linked to CommentId).
            var commentIds = comments.Select(c => c.Id).Distinct().ToList();
            if (commentIds.Count > 0)
            {
                var attachmentEntities = await _context.Attachments
                    .Where(a => commentIds.Contains(a.CommentId))
                    .ToListAsync();

                ticketDto.Attachments = _mapper.Map<List<Attachment>, List<AttachmentDTO>>(attachmentEntities);
            }

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

            if (filters.ProductId.HasValue)
            {
                query = query.Where(t => t.ProductId == filters.ProductId);
            }

            if (filters.Category.HasValue)
            {
                query = query.Where(t => t.Category == filters.Category);
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
                .Include(t => t.AssignedTo)
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
                    UserEmail = t.User != null ? t.User.Email : "",
                    t.AssignedToId,
                    AssignedToName = t.AssignedTo != null ? $"{(t.AssignedTo.Name ?? "").Trim()} {(t.AssignedTo.Surname ?? "").Trim()}".Trim() : null
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
            ticket.UpdatedAt = DateTimeOffset.UtcNow;
            ticket.AssignedToId = ticketDTO.AssignedToId;
            ticket.ResolutionDueAt = CalculateResolutionDueAt(ticket.Priority, ticket.CreatedAt);

            if(ticketDTO.Status == Status.Closed)
            {
                ticket.ClosedAt = DateTimeOffset.UtcNow;
                ticket.IsSlaBreached = ticket.ResolutionDueAt.HasValue && ticket.ClosedAt > ticket.ResolutionDueAt;
            }
            else
            {
                ticket.ClosedAt = null;
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
            ticket.UpdatedAt = DateTimeOffset.UtcNow;

            if (status == Status.Closed)
            {
                ticket.ClosedAt = DateTimeOffset.UtcNow;
                ticket.IsSlaBreached = ticket.ResolutionDueAt.HasValue && ticket.ClosedAt > ticket.ResolutionDueAt;
            }
            else
            {
                ticket.ClosedAt = null;
            }

            await _context.SaveChangesAsync();
        }

        public async Task AssignTicket(Guid ticketId, Guid? agentId)
        {
            var ticketSnapshot = await _context.Tickets
                .Where(t => t.Id == ticketId)
                .Select(t => new
                {
                    t.Id,
                    t.Status
                })
                .FirstOrDefaultAsync();

            if (ticketSnapshot == null)
                throw new NotFoundException();

            var nextStatus = ticketSnapshot.Status;

            // Nếu gán agent và ticket đang ở trạng thái Open, chuyển sang InProgress
            if (agentId.HasValue && ticketSnapshot.Status == Status.Open)
            {
                nextStatus = Status.InProgress;
            }

            await _context.Tickets
                .Where(t => t.Id == ticketId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.AssignedToId, agentId)
                    .SetProperty(t => t.UpdatedAt, DateTimeOffset.UtcNow)
                    .SetProperty(t => t.Status, nextStatus));

            if (agentId.HasValue)
            {
                await CreateNotificationAsync(
                    agentId.Value,
                    ticketId,
                    "ticket_assigned",
                    "Ticket duoc gan cho ban",
                    $"Ticket #{ticketId.ToString()[..8].ToUpper()} vua duoc gan cho ban.");
            }
        }

        public async Task<object> GetAgentStats(Guid agentId)
        {
            await ProcessSlaBreachesAsync();

            var now = DateTimeOffset.Now;
            var today = now.Date;
            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var assignedTickets = await _context.Tickets
                .Where(t => t.AssignedToId == agentId)
                .Select(t => new
                {
                    t.Id,
                    t.Status,
                    t.Priority,
                    t.Category,
                    t.SentimentScore,
                    t.CreatedAt,
                    t.UpdatedAt,
                    t.ClosedAt
                })
                .ToListAsync();

            var totalAssigned = assignedTickets.Count;
            var resolved = assignedTickets.Count(t => t.Status == Status.Closed);
            var inProgress = assignedTickets.Count(t => t.Status == Status.InProgress);
            var open = assignedTickets.Count(t => t.Status == Status.Open);

            var resolvedToday = assignedTickets.Count(t =>
                t.Status == Status.Closed && t.ClosedAt.HasValue &&
                t.ClosedAt.Value.Date == today);

            var resolvedThisWeek = assignedTickets.Count(t =>
                t.Status == Status.Closed && t.ClosedAt.HasValue &&
                t.ClosedAt.Value.Date >= weekStart);

            var resolvedThisMonth = assignedTickets.Count(t =>
                t.Status == Status.Closed && t.ClosedAt.HasValue &&
                t.ClosedAt.Value.Date >= monthStart);

            var resolutionRate = totalAssigned > 0
                ? Math.Round((double)resolved / totalAssigned * 100, 1)
                : 0;

            return new
            {
                TotalAssigned = totalAssigned,
                Resolved = resolved,
                InProgress = inProgress,
                Open = open,
                ResolvedToday = resolvedToday,
                ResolvedThisWeek = resolvedThisWeek,
                ResolvedThisMonth = resolvedThisMonth,
                ResolutionRate = resolutionRate
            };
        }

        // ✅ AGENT DASHBOARD: Smart Queue - Danh sách tickets đã được gán cho agent
        public async Task<AgentSmartQueueDTO> GetAgentSmartQueue(Guid agentId, AgentTicketFiltersDTO filters)
        {
            await ProcessSlaBreachesAsync();

            var now = DateTimeOffset.Now;
            var query = _context.Tickets
                .Where(t => EF.Property<Guid?>(t, nameof(Ticket.UserId)) != null)
                .Where(t => t.AssignedToId == agentId)
                .AsQueryable();

            // Apply filters
            if (filters.Priority.HasValue)
                query = query.Where(t => t.Priority == filters.Priority);

            if (filters.Status.HasValue)
                query = query.Where(t => t.Status == filters.Status);

            if (filters.ProductId.HasValue)
                query = query.Where(t => t.ProductId == filters.ProductId);

            if (filters.Category.HasValue)
                query = query.Where(t => t.Category == filters.Category);

            if (!string.IsNullOrWhiteSpace(filters.SentimentLabel))
                query = query.Where(t => t.SentimentLabel == filters.SentimentLabel);

            // Count after filtering
            var total = await query.CountAsync();

            // Sort by Priority (High first) + CreatedAt (oldest first)
            var queueDTOs = await query
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.CreatedAt)
                .Skip(filters.Skip)
                .Take(filters.Take)
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    t.Priority,
                    t.Status,
                    t.Category,
                    t.SentimentLabel,
                    t.SentimentScore,
                    t.ResolutionDueAt,
                    t.IsSlaBreached,
                    t.ProductName,
                    t.ProductId,
                    t.AssignedToId,
                    CustomerName = t.User != null ? t.User.Name : "",
                    CustomerEmail = t.User != null ? t.User.Email : "",
                    AssignedToName = t.AssignedTo != null ? t.AssignedTo.Name : "",
                    t.CreatedAt,
                    CommentsCount = _context.Comments.Count(c => c.TicketId == t.Id)
                })
                .ToListAsync();

            var mappedQueue = queueDTOs.Select(t => new AgentQueueTicketDTO
            {
                Id = t.Id,
                Title = t.Title,
                Priority = t.Priority,
                Status = t.Status,
                Category = t.Category,
                SentimentLabel = t.SentimentLabel,
                SentimentScore = t.SentimentScore,
                CustomerName = t.CustomerName ?? "",
                CustomerEmail = t.CustomerEmail ?? "",
                ProductName = t.ProductName,
                ProductId = t.ProductId,
                AssignedToName = t.AssignedToName ?? "",
                AssignedToId = t.AssignedToId,
                CreatedAt = t.CreatedAt,
                ResolutionDueAt = t.ResolutionDueAt,
                IsSlaBreached = t.IsSlaBreached,
                WaitingMinutes = (int)(now - t.CreatedAt).TotalMinutes,
                CommentsCount = t.CommentsCount,
                WeightScore = (int)t.Priority + ((int)(now - t.CreatedAt).TotalMinutes / 100)
            }).ToList();

            // Statistics (aggregate trực tiếp để tránh materialize toàn bộ entity)
            var highPriorityCount = await _context.Tickets
                .CountAsync(t => EF.Property<Guid?>(t, nameof(Ticket.UserId)) != null && t.AssignedToId == agentId && t.Priority == Priority.High);
            var negativeSentimentCount = await _context.Tickets
                .CountAsync(t => EF.Property<Guid?>(t, nameof(Ticket.UserId)) != null && t.AssignedToId == agentId && !string.IsNullOrEmpty(t.SentimentLabel) && t.SentimentLabel == "negative");
            var unassignedCount = await _context.Tickets
                .CountAsync(t => EF.Property<Guid?>(t, nameof(Ticket.UserId)) != null && t.AssignedToId == null);

            var stats = new AgentSmartQueueDTO
            {
                Tickets = mappedQueue,
                Total = total,
                Take = filters.Take,
                Skip = filters.Skip,
                HighPriorityCount = highPriorityCount,
                NegativeSentimentCount = negativeSentimentCount,
                UnassignedCount = unassignedCount
            };

            return stats;
        }

        // ✅ AGENT DASHBOARD: Unassigned Tickets (Nhân viên có thể chọn để gán cho mình)
        public async Task<AgentSmartQueueDTO> GetUnassignedTickets(AgentTicketFiltersDTO filters)
        {
            await ProcessSlaBreachesAsync();

            var now = DateTimeOffset.Now;
            var query = _context.Tickets
                .Where(t => EF.Property<Guid?>(t, nameof(Ticket.UserId)) != null)
                .Where(t => t.AssignedToId == null)
                .AsQueryable();

            // Apply filters
            if (filters.Priority.HasValue)
                query = query.Where(t => t.Priority == filters.Priority);

            if (filters.Status.HasValue)
                query = query.Where(t => t.Status == filters.Status);

            if (filters.ProductId.HasValue)
                query = query.Where(t => t.ProductId == filters.ProductId);

            if (filters.Category.HasValue)
                query = query.Where(t => t.Category == filters.Category);

            if (!string.IsNullOrWhiteSpace(filters.SentimentLabel))
                query = query.Where(t => t.SentimentLabel == filters.SentimentLabel);

            // Count after filtering
            var total = await query.CountAsync();

            // Sort by Priority (High first) + negative sentiment + waiting time
            var queueDTOs = await query
                .OrderByDescending(t => t.Priority)
                .ThenByDescending(t => !string.IsNullOrEmpty(t.SentimentLabel) && t.SentimentLabel == "negative")
                .ThenBy(t => t.CreatedAt)
                .Skip(filters.Skip)
                .Take(filters.Take)
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    t.Priority,
                    t.Status,
                    t.Category,
                    t.SentimentLabel,
                    t.SentimentScore,
                    t.ResolutionDueAt,
                    t.IsSlaBreached,
                    t.ProductName,
                    t.ProductId,
                    CustomerName = t.User != null ? t.User.Name : "",
                    CustomerEmail = t.User != null ? t.User.Email : "",
                    t.CreatedAt,
                    CommentsCount = _context.Comments.Count(c => c.TicketId == t.Id)
                })
                .ToListAsync();

            var mappedQueue = queueDTOs.Select(t => new AgentQueueTicketDTO
            {
                Id = t.Id,
                Title = t.Title,
                Priority = t.Priority,
                Status = t.Status,
                Category = t.Category,
                SentimentLabel = t.SentimentLabel,
                SentimentScore = t.SentimentScore,
                CustomerName = t.CustomerName ?? "",
                CustomerEmail = t.CustomerEmail ?? "",
                ProductName = t.ProductName,
                ProductId = t.ProductId,
                AssignedToName = null,
                AssignedToId = null,
                CreatedAt = t.CreatedAt,
                ResolutionDueAt = t.ResolutionDueAt,
                IsSlaBreached = t.IsSlaBreached,
                WaitingMinutes = (int)(now - t.CreatedAt).TotalMinutes,
                CommentsCount = t.CommentsCount,
                WeightScore = (int)t.Priority + (t.SentimentLabel == "negative" ? 10 : 0) + ((int)(now - t.CreatedAt).TotalMinutes / 100)
            }).ToList();

            // Statistics (aggregate trực tiếp để tránh materialize toàn bộ entity)
            var highPriorityCount = await _context.Tickets
                .CountAsync(t => EF.Property<Guid?>(t, nameof(Ticket.UserId)) != null && t.AssignedToId == null && t.Priority == Priority.High);
            var negativeSentimentCount = await _context.Tickets
                .CountAsync(t => EF.Property<Guid?>(t, nameof(Ticket.UserId)) != null && t.AssignedToId == null && !string.IsNullOrEmpty(t.SentimentLabel) && t.SentimentLabel == "negative");
            var unassignedCount = await _context.Tickets
                .CountAsync(t => EF.Property<Guid?>(t, nameof(Ticket.UserId)) != null && t.AssignedToId == null);

            var stats = new AgentSmartQueueDTO
            {
                Tickets = mappedQueue,
                Total = total,
                Take = filters.Take,
                Skip = filters.Skip,
                HighPriorityCount = highPriorityCount,
                NegativeSentimentCount = negativeSentimentCount,
                UnassignedCount = unassignedCount
            };

            return stats;
        }

        // ✅ ADMIN DASHBOARD: Tổng hợp tất cả metrics
        public async Task<AdminDashboardDTO> GetAdminDashboard(int days = 30, Guid? agentId = null)
        {
            await ProcessSlaBreachesAsync();

            var safeDays = Math.Clamp(days, 1, 365);
            var periodStart = DateTimeOffset.UtcNow.Date.AddDays(-safeDays + 1);

            var allTickets = await _context.Tickets
                .Include(t => t.Product)
                .Include(t => t.Comments)
                .Include(t => t.AssignedTo)
                .Include(t => t.Feedback)
                .ToListAsync();

            var periodTickets = allTickets
                .Where(t => t.CreatedAt >= periodStart)
                .ToList();

            if (agentId.HasValue)
            {
                periodTickets = periodTickets
                    .Where(t => t.AssignedToId == agentId.Value)
                    .ToList();
            }

            // Basic stats
            var dashboard = new AdminDashboardDTO
            {
                TotalTickets = periodTickets.Count,
                OpenTickets = periodTickets.Count(t => t.Status == Status.Open),
                InProgressTickets = periodTickets.Count(t => t.Status == Status.InProgress),
                ClosedTickets = periodTickets.Count(t => t.Status == Status.Closed),
                SlaBreachedTickets = periodTickets.Count(t => t.IsSlaBreached),

                // Sentiment stats
                PositiveSentimentCount = periodTickets.Count(t => !string.IsNullOrEmpty(t.SentimentLabel) && t.SentimentLabel == "positive"),
                NegativeSentimentCount = periodTickets.Count(t => !string.IsNullOrEmpty(t.SentimentLabel) && t.SentimentLabel == "negative"),
                NeutralSentimentCount = periodTickets.Count(t => !string.IsNullOrEmpty(t.SentimentLabel) && t.SentimentLabel == "neutral"),
                AverageSentimentScore = periodTickets.Count > 0 
                    ? (float)periodTickets.Average(t => t.SentimentScore ?? 0.5f)
                    : 0.5f,

                // Priority stats
                HighPriorityCount = periodTickets.Count(t => t.Priority == Priority.High),
                MediumPriorityCount = periodTickets.Count(t => t.Priority == Priority.Medium),
                LowPriorityCount = periodTickets.Count(t => t.Priority == Priority.Low),

                // Category stats
                BugCount = periodTickets.Count(t => t.Category == Category.Bug),
                FeatureCount = periodTickets.Count(t => t.Category == Category.Feature),
                SupportCount = periodTickets.Count(t => t.Category == Category.Support),
                SaleCount = periodTickets.Count(t => t.Category == Category.Sale),
                FeedbackCount = periodTickets.Count(t => t.Feedback != null),
                AverageCsatRating = periodTickets.Any(t => t.Feedback != null)
                    ? (float)periodTickets.Where(t => t.Feedback != null).Average(t => t.Feedback!.Rating)
                    : 0
            };

            var aiLogs = await _context.AiSuggestionLogs
                .AsNoTracking()
                .Where(x => x.CreatedAt >= periodStart)
                .ToListAsync();

            if (agentId.HasValue)
            {
                aiLogs = aiLogs
                    .Where(x => x.AgentId == agentId.Value)
                    .ToList();
            }

            var feedbackLogs = aiLogs.Where(x => x.IsHelpful.HasValue).ToList();

            dashboard.AiSuggestionsGenerated = aiLogs.Count;
            dashboard.AiSuggestionsAccepted = aiLogs.Count(x => x.IsAccepted);
            dashboard.AiSuggestionsHelpful = aiLogs.Count(x => x.IsHelpful == true);
            dashboard.AiSuggestionsNotHelpful = aiLogs.Count(x => x.IsHelpful == false);
            dashboard.AiAcceptanceRate = dashboard.AiSuggestionsGenerated > 0
                ? (float)Math.Round((double)dashboard.AiSuggestionsAccepted / dashboard.AiSuggestionsGenerated * 100, 2)
                : 0;
            dashboard.AiHelpfulnessRate = feedbackLogs.Count > 0
                ? (float)Math.Round((double)dashboard.AiSuggestionsHelpful / feedbackLogs.Count * 100, 2)
                : 0;

            dashboard.AiSourceStats = aiLogs
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Source) ? "unknown" : x.Source)
                .Select(g => new AiSourceStatDTO
                {
                    Source = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            dashboard.AiSuggestionTrends = Enumerable.Range(0, safeDays)
                .Select(i =>
                {
                    var date = periodStart.Date.AddDays(i);
                    var generated = aiLogs.Count(x => x.CreatedAt.Date == date);
                    var accepted = aiLogs.Count(x => x.AcceptedAt.HasValue && x.AcceptedAt.Value.Date == date);

                    return new AiSuggestionTrendDTO
                    {
                        Date = date,
                        GeneratedCount = generated,
                        AcceptedCount = accepted
                    };
                })
                .ToList();

            // Product stats scoped to selected period/agent filter.
            dashboard.ProductStats = periodTickets
                .GroupBy(t => new { t.ProductId, t.ProductName })
                .Select(g => new ProductStatDTO
                {
                    ProductId = g.Key.ProductId ?? Guid.Empty,
                    ProductName = string.IsNullOrWhiteSpace(g.Key.ProductName) ? "Khong xac dinh" : g.Key.ProductName,
                    TotalTickets = g.Count(),
                    OpenTickets = g.Count(t => t.Status == Status.Open),
                    PositiveSentimentPercentage = g.Count() > 0
                        ? (float)g.Count(t => !string.IsNullOrEmpty(t.SentimentLabel) && t.SentimentLabel == "positive") / g.Count() * 100
                        : 0,
                    NegativeSentimentPercentage = g.Count() > 0
                        ? (float)g.Count(t => !string.IsNullOrEmpty(t.SentimentLabel) && t.SentimentLabel == "negative") / g.Count() * 100
                        : 0
                })
                .OrderByDescending(x => x.TotalTickets)
                .ToList();

            // Agent performance stats
            // Get all unique agents (users who have assigned tickets)
            var agentIds = periodTickets
                .Where(t => t.AssignedToId.HasValue)
                .Select(t => t.AssignedToId.Value)
                .Distinct()
                .ToList();

            var agents = await _context.Users
                .Where(u => agentIds.Contains(u.Id))
                .ToListAsync();

            if (agentId.HasValue && agents.Count == 0)
            {
                var agent = await _context.Users.FirstOrDefaultAsync(u => u.Id == agentId.Value);
                if (agent != null)
                {
                    agents = new List<User> { agent };
                }
            }

            dashboard.AgentStats = agents.Select(agent => new AgentPerformanceDTO
            {
                AgentId = agent.Id,
                AgentName = $"{(agent.Name ?? "").Trim()} {(agent.Surname ?? "").Trim()}".Trim(),
                AssignedTickets = periodTickets.Count(t => t.AssignedToId == agent.Id),
                ClosedTickets = periodTickets.Count(t => t.AssignedToId == agent.Id && t.Status == Status.Closed),
                OpenTickets = periodTickets.Count(t => t.AssignedToId == agent.Id && t.Status == Status.Open),
                AverageResolutionTimeHours = (float)periodTickets
                    .Where(t => t.AssignedToId == agent.Id && t.ClosedAt.HasValue)
                    .Select(t => (t.ClosedAt!.Value - t.CreatedAt).TotalHours)
                    .DefaultIfEmpty(0)
                    .Average(),
                AverageCsatRating = (float)periodTickets
                    .Where(t => t.AssignedToId == agent.Id && t.Feedback != null)
                    .Select(t => t.Feedback!.Rating)
                    .DefaultIfEmpty(0)
                    .Average(),
                CustomerSatisfactionPercentage = (float)(periodTickets
                    .Where(t => t.AssignedToId == agent.Id && t.Feedback != null)
                    .Select(t => t.Feedback!.Rating)
                    .DefaultIfEmpty(0)
                    .Average() * 20.0)
            }).ToList();

            // Ticket trends (selected range)
            var today = DateTimeOffset.Now.Date;
            var trendStartDate = today.AddDays(-safeDays + 1);

            dashboard.TicketTrends = Enumerable.Range(0, safeDays)
                .Select(i => 
                {
                    var date = trendStartDate.AddDays(i);
                    var ticketsOnDate = periodTickets.Where(t => t.CreatedAt.Date == date).ToList();
                    var closedOnDate = periodTickets.Where(t => t.ClosedAt?.Date == date).ToList();
                    var totalOpenOnDate = periodTickets.Count(t => 
                        t.CreatedAt.Date <= date && 
                        (t.Status != Status.Closed || t.ClosedAt?.Date > date));

                    return new TicketTrendDTO
                    {
                        Date = date,
                        NewTickets = ticketsOnDate.Count,
                        ClosedTickets = closedOnDate.Count,
                        TotalOpen = totalOpenOnDate
                    };
                })
                .ToList();

            return dashboard;
        }

        public async Task<TicketFeedbackDTO> SubmitTicketFeedback(Guid ticketId, Guid userId, SubmitTicketFeedbackDTO dto)
        {
            if (dto.Rating < 1 || dto.Rating > 5)
            {
                throw new ArgumentException("Rating must be in range 1..5");
            }

            var ticket = await _context.Tickets
                .Where(t => t.Id == ticketId)
                .Select(t => new { t.Id, t.UserId, t.Status })
                .FirstOrDefaultAsync();

            if (ticket == null)
                throw new NotFoundException();

            if (ticket.UserId != userId)
                throw new ForbiddenException();

            if (ticket.Status != Status.Closed)
                throw new InvalidOperationException("Only closed tickets can be rated.");

            var existing = await _context.TicketFeedbacks.FirstOrDefaultAsync(x => x.TicketId == ticketId);
            if (existing == null)
            {
                existing = new TicketFeedback
                {
                    Id = Guid.NewGuid(),
                    TicketId = ticketId,
                    UserId = userId,
                    Rating = dto.Rating,
                    Comment = dto.Comment?.Trim(),
                    CreatedAt = DateTimeOffset.UtcNow
                };

                _context.TicketFeedbacks.Add(existing);
            }
            else
            {
                existing.Rating = dto.Rating;
                existing.Comment = dto.Comment?.Trim();
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await _context.SaveChangesAsync();

            return new TicketFeedbackDTO
            {
                Id = existing.Id,
                TicketId = existing.TicketId,
                UserId = existing.UserId,
                Rating = existing.Rating,
                Comment = existing.Comment,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = existing.UpdatedAt
            };
        }

        public async Task<TicketFeedbackDTO?> GetTicketFeedback(Guid ticketId)
        {
            return await _context.TicketFeedbacks
                .Where(x => x.TicketId == ticketId)
                .Select(x => new TicketFeedbackDTO
                {
                    Id = x.Id,
                    TicketId = x.TicketId,
                    UserId = x.UserId,
                    Rating = x.Rating,
                    Comment = x.Comment,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .FirstOrDefaultAsync();
        }

        public async Task<int> ProcessSlaBreachesAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var breachedTickets = await _context.Tickets
                .Where(t => t.Status != Status.Closed && t.ResolutionDueAt.HasValue && t.ResolutionDueAt < now && !t.IsSlaBreached)
                .Select(t => new { t.Id, t.AssignedToId })
                .ToListAsync();

            if (breachedTickets.Count == 0)
                return 0;

            var breachedIds = breachedTickets.Select(t => t.Id).ToList();
            await _context.Tickets
                .Where(t => breachedIds.Contains(t.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.IsSlaBreached, true)
                    .SetProperty(t => t.UpdatedAt, now));

            foreach (var ticket in breachedTickets.Where(t => t.AssignedToId.HasValue))
            {
                var alreadyNotified = await _context.UserNotifications.AnyAsync(n =>
                    n.UserId == ticket.AssignedToId!.Value &&
                    n.TicketId == ticket.Id &&
                    n.Type == "sla_breach");

                if (!alreadyNotified)
                {
                    _context.UserNotifications.Add(new UserNotification
                    {
                        Id = Guid.NewGuid(),
                        UserId = ticket.AssignedToId!.Value,
                        TicketId = ticket.Id,
                        Type = "sla_breach",
                        Title = "Canh bao SLA",
                        Message = $"Ticket #{ticket.Id.ToString()[..8].ToUpper()} da qua han SLA.",
                        IsRead = false,
                        CreatedAt = now
                    });
                }
            }

            await _context.SaveChangesAsync();
            return breachedTickets.Count;
        }
    }
}
