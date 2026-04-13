namespace SmartHelpdesk.DTOs.Requests
{
    public class UpdateProductAssignmentsDTO
    {
        public List<Guid> AgentIds { get; set; } = new();
    }
}
