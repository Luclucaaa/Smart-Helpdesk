namespace SmartHelpdesk.Data.Entities
{
    public class TicketFeedback
    {
        public Guid Id { get; set; }
        public Guid TicketId { get; set; }
        public Guid UserId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public Ticket Ticket { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
