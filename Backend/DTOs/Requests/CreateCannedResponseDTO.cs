using SmartHelpdesk.Data.Enums;

namespace SmartHelpdesk.DTOs.Requests
{
    /// <summary>
    /// Tạo Canned Response (mẫu trả lời nhanh)
    /// </summary>
    public class CreateCannedResponseDTO
    {
        public string Title { get; set; } = null!;  // VD: "Hướng dẫn cài đặt"
        public string Text { get; set; } = null!;   // Nội dung mẫu
        public Category? Category { get; set; }      // Áp dụng cho Bug/Feature/Sale nào?
        public Guid? CreatedBy { get; set; }        // User ID người tạo
    }
}
