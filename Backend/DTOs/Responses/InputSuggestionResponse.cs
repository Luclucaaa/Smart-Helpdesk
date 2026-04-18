namespace SmartHelpdesk.DTOs.Responses;

/// <summary>
/// Response tra ve danh sach goi y noi dung nhap lieu
/// </summary>
public class InputSuggestionResponse
{
    public List<string> Suggestions { get; set; } = new();

    /// <summary>
    /// Nguon sinh goi y: gemini hoac fallback
    /// </summary>
    public string Source { get; set; } = "fallback";
}
