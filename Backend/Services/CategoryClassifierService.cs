using SmartHelpdesk.Data.Enums;
using SmartHelpdesk.DTOs.Responses;
using SmartHelpdesk.Interfaces;

namespace SmartHelpdesk.Services
{
    public class CategoryClassifierService : ICategoryClassifierService
    {
        private static readonly Dictionary<Category, string[]> Keywords = new()
        {
            [Category.Bug] = new[]
            {
                "lỗi", "bug", "error", "khong dang nhap duoc", "không đăng nhập", "crash", "sập", "treo",
                "khong hoat dong", "không hoạt động", "khong tai duoc", "không tải được", "trang trắng"
            },
            [Category.Feature] = new[]
            {
                "tính năng", "tinh nang", "đề xuất", "de xuat", "nâng cấp", "mo rong", "mở rộng",
                "cần thêm", "bo sung", "bổ sung", "yêu cầu mới", "yeu cau moi"
            },
            [Category.Sale] = new[]
            {
                "báo giá", "bao gia", "gói", "goi", "mua", "gia han", "gia hạn", "thanh toán", "thanh toan",
                "hợp đồng", "hop dong", "chi phí", "chi phi", "khuyến mãi", "khuyen mai"
            },
            [Category.Support] = new[]
            {
                "hướng dẫn", "huong dan", "cài đặt", "cai dat", "cấu hình", "cau hinh", "tư vấn", "tu van",
                "không biết", "khong biet", "cách dùng", "cach dung", "help", "hỗ trợ", "ho tro"
            }
        };

        public CategoryClassificationDTO Classify(string description, string? title = null, string? productName = null)
        {
            var text = string.Join(" ", new[] { title ?? string.Empty, description ?? string.Empty, productName ?? string.Empty })
                .ToLowerInvariant();

            var scores = new Dictionary<Category, int>
            {
                [Category.Bug] = 0,
                [Category.Feature] = 0,
                [Category.Support] = 0,
                [Category.Sale] = 0
            };

            foreach (var pair in Keywords)
            {
                var score = 0;
                foreach (var keyword in pair.Value)
                {
                    if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        score += keyword.Length > 7 ? 2 : 1;
                    }
                }

                scores[pair.Key] = score;
            }

            var best = scores.OrderByDescending(x => x.Value).First();
            if (best.Value == 0)
            {
                return new CategoryClassificationDTO
                {
                    Category = Category.Support,
                    Confidence = 0.4f,
                    Reason = "Khong tim thay tu khoa manh, mac dinh la ho tro."
                };
            }

            var total = scores.Values.Sum();
            var confidence = total > 0 ? Math.Clamp((float)best.Value / total, 0.45f, 0.98f) : 0.4f;

            return new CategoryClassificationDTO
            {
                Category = best.Key,
                Confidence = confidence,
                Reason = $"Phat hien {best.Value} diem tu khoa cho nhom {best.Key}."
            };
        }
    }
}
