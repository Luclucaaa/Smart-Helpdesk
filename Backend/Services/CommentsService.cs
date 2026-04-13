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
    public class CommentsService : ICommentsService
    {
        private readonly SmartHelpdeskContext _context;
        private readonly IMapper _mapper;

        public CommentsService(SmartHelpdeskContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<Guid> CreateComment(CreateCommentDTO commentDTO)
        {
            var newComment = _mapper.Map<CreateCommentDTO, Comment>(commentDTO);
            newComment.CreatedAt = DateTimeOffset.UtcNow;

            var ticketContext = await _context.Tickets
                .Where(t => t.Id == commentDTO.TicketId)
                .Select(t => new
                {
                    t.Id,
                    t.UserId,
                    t.Status,
                    t.FirstResponseAt,
                    t.ResolutionDueAt,
                    t.IsSlaBreached
                })
                .FirstOrDefaultAsync();

            if (ticketContext == null)
            {
                throw new NotFoundException();
            }

            _context.Comments.Add(newComment);

            var shouldSetFirstResponse = ticketContext.FirstResponseAt == null && ticketContext.UserId != commentDTO.UserId;
            if (shouldSetFirstResponse)
            {
                var now = DateTimeOffset.UtcNow;
                var isSlaBreached = ticketContext.ResolutionDueAt.HasValue && now > ticketContext.ResolutionDueAt.Value;

                await _context.Tickets
                    .Where(t => t.Id == ticketContext.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(t => t.FirstResponseAt, now)
                        .SetProperty(t => t.Status, ticketContext.Status == Status.Open ? Status.InProgress : ticketContext.Status)
                        .SetProperty(t => t.IsSlaBreached, ticketContext.IsSlaBreached || isSlaBreached)
                        .SetProperty(t => t.UpdatedAt, now));
            }

            await _context.SaveChangesAsync();

            return newComment.Id;
        }

        public async Task DeleteComment(Guid id)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null)
            {
                throw new NotFoundException();
            }
            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();
        }

        public async Task<CommentDTO> GetComment(Guid id)
        {
            var comments = await _context.Comments
               .Include(comment => comment.User)
               .Include(comment => comment.Ticket)
               .ToListAsync();

            var comment = comments.FirstOrDefault(t => t.Id == id);
            if (comment == null)
            {
                throw new NotFoundException();
            }

            var commentDto = _mapper.Map<Comment, CommentDTO>(comment);

            return commentDto;
        }

        public async Task<IEnumerable<CommentDTO>> GetCommentsToTicket(Guid ticketId)
        {
            var ticket = await _context.Tickets
            .Include(t => t.Comments)
                .ThenInclude(c => c.User)
            .SingleOrDefaultAsync(t => t.Id == ticketId);

            if (ticket == null)
            {
                throw new NotFoundException();
            }

            var comments = ticket.Comments;

            var commentsDTOs = _mapper.Map<List<Comment>, List<CommentDTO>>(comments);

            return commentsDTOs;
        }

        public async Task UpdateComment(Guid id, UpdateCommentDTO commentDTO)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null)
            {
                throw new NotFoundException();
            }

            comment.Text = commentDTO.Text;
            comment.UpdatedAt = DateTimeOffset.Now;

            await _context.SaveChangesAsync();
        }
    }
}
