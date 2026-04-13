namespace SmartHelpdesk.DTOs.Requests
{
    public class SubmitTicketFeedbackDTO
    {
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }
}
