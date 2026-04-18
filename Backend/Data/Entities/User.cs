using Microsoft.AspNetCore.Identity;

namespace SmartHelpdesk.Data.Entities
{
    public class User : IdentityUser<Guid>
    {
        public string Name { get; set; } = null!;
        public string Surname { get; set; } = null!;
        public List<Ticket> CreatedTickets { get; set; } = new List<Ticket>();
        public List<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
        public List<Comment> Comments { get; set; } = new List<Comment>();
        public List<ProductAgentAssignment> ProductAssignments { get; set; } = new List<ProductAgentAssignment>();
        public List<TicketFeedback> TicketFeedbacks { get; set; } = new List<TicketFeedback>();
        public List<UserNotification> Notifications { get; set; } = new List<UserNotification>();
        public List<AiSuggestionLog> AiSuggestionLogs { get; set; } = new List<AiSuggestionLog>();
    }
}
