using SmartHelpdesk.DTOs.Responses;

namespace SmartHelpdesk.Interfaces
{
    public interface ICategoryClassifierService
    {
        CategoryClassificationDTO Classify(string description, string? title = null, string? productName = null);
    }
}
