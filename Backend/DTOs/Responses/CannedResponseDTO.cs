using SmartHelpdesk.Data.Enums;

namespace SmartHelpdesk.DTOs.Responses
{
    public class CannedResponseDTO
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string Text { get; set; } = null!;
        public Category? Category { get; set; }
        public string? CreatedByName { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
