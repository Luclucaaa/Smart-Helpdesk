using SmartHelpdesk.Data.Entities;

namespace SmartHelpdesk.DTOs.Responses
{
    public class CommentDTO
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; } = null!;
        public string UserEmail { get; set; } = null!;
        public bool IsFromAgent { get; set; }
        public Guid TicketId { get; set; }
        public string TicketTitle { get; set; } = null!;
    }
}
