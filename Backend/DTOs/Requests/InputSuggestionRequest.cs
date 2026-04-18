namespace SmartHelpdesk.DTOs.Requests;

/// <summary>
/// Request body cho API goi y noi dung nhap lieu ticket
/// </summary>
public class InputSuggestionRequest
{
    /// <summary>
    /// Noi dung ban nhap tam thoi (co the rong)
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Ten san pham khach hang dang su dung (optional)
    /// </summary>
    public string? ProductName { get; set; }

    /// <summary>
    /// So goi y toi da tra ve (mac dinh 3)
    /// </summary>
    public int MaxSuggestions { get; set; } = 3;
}
