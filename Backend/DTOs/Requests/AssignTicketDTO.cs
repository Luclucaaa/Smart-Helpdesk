namespace SmartHelpdesk.DTOs.Requests
{
    public class AssignTicketDTO
    {
        public Guid? AgentId { get; set; }  // null = unassign
    }
}
