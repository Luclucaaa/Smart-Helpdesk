using SmartHelpdesk.Data.Enums;

namespace SmartHelpdesk.DTOs.Responses
{
    public class CategoryClassificationDTO
    {
        public Category Category { get; set; }
        public float Confidence { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
