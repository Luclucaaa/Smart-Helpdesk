using SmartHelpdesk.Data.Entities;
using SmartHelpdesk.Data.Enums;

namespace SmartHelpdesk.DTOs.Requests
{
    public class CreateTicketDTO
    {
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public Priority Priority { get; set; }
        public Guid UserId { get; set; }
        public Guid? AssignedToId { get; set; }
    }
}
