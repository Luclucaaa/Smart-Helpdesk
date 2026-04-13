namespace SmartHelpdesk.DTOs.Responses
{
    public class TicketFeedbackDTO
    {
        public Guid Id { get; set; }
        public Guid TicketId { get; set; }
        public Guid UserId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
