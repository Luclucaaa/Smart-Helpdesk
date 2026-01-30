using SmartHelpdesk.Data.Entities;
using SmartHelpdesk.Data.Enums;

namespace SmartHelpdesk.DTOs.Requests
{
    public class CreateTicketDTO
    {
        public string? Title { get; set; }  // Tùy chọn - sẽ tự động tạo từ Description nếu null
        public string Description { get; set; } = null!;
        public Priority Priority { get; set; } = Priority.Medium;  // Mặc định là Medium
        public Guid UserId { get; set; }
        public Guid? AssignedToId { get; set; }
        public Guid? ProductId { get; set; }  // Tùy chọn
        public string? ProductName { get; set; }  // Tên sản phẩm do khách hàng nhập (tùy chọn)
    }
}
