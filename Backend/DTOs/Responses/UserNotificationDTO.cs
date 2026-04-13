namespace SmartHelpdesk.DTOs.Responses
{
    public class UserNotificationDTO
    {
        public Guid Id { get; set; }
        public Guid? TicketId { get; set; }
        public string Type { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public bool IsRead { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ReadAt { get; set; }
    }
}
