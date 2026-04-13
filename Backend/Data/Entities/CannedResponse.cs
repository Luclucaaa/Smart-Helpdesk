using SmartHelpdesk.Data.Enums;

namespace SmartHelpdesk.Data.Entities
{
    /// <summary>
    /// Mẫu trả lời nhanh cho nhân viên hỗ trợ
    /// </summary>
    public class CannedResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;  // VD: "Hướng dẫn cài đặt", "Tôi sẽ kiểm tra"
        public string Text { get; set; } = null!;   // Nội dung mẫu đầy đủ
        public Category? Category { get; set; }      // Áp dụng cho Bug/Feature/Sale nào? (tùy chọn)
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;  // Có còn sử dụng không?
        
        // Foreign Keys
        public Guid? CreatedByUserId { get; set; }
        
        // Navigation
        public User? CreatedBy { get; set; }
    }
}
