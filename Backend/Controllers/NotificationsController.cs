using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHelpdesk.Common.Identity;
using SmartHelpdesk.Data;
using SmartHelpdesk.Data.Entities;
using SmartHelpdesk.DTOs.Responses;

namespace SmartHelpdesk.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly SmartHelpdeskContext _context;
        private readonly UserManager<User> _userManager;

        public NotificationsController(SmartHelpdeskContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyNotifications([FromQuery] bool unreadOnly = false, [FromQuery] int take = 50)
        {
            var user = await _userManager.GetCurrentUserAsync(User);
            if (user == null)
            {
                return Unauthorized("Vui lòng đăng nhập");
            }

            take = Math.Clamp(take, 1, 200);

            var query = _context.UserNotifications
                .Where(n => n.UserId == user.Id)
                .AsQueryable();

            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }

            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(take)
                .Select(n => new UserNotificationDTO
                {
                    Id = n.Id,
                    TicketId = n.TicketId,
                    Type = n.Type,
                    Title = n.Title,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt,
                    ReadAt = n.ReadAt
                })
                .ToListAsync();

            var unreadCount = await _context.UserNotifications.CountAsync(n => n.UserId == user.Id && !n.IsRead);

            return Ok(new
            {
                total = items.Count,
                unreadCount,
                notifications = items
            });
        }

        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkAsRead(Guid id)
        {
            var user = await _userManager.GetCurrentUserAsync(User);
            if (user == null)
            {
                return Unauthorized("Vui lòng đăng nhập");
            }

            var updated = await _context.UserNotifications
                .Where(n => n.Id == id && n.UserId == user.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow));

            if (updated == 0)
            {
                return NotFound();
            }

            return Ok(new { message = "Đã đánh dấu đã đọc" });
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var user = await _userManager.GetCurrentUserAsync(User);
            if (user == null)
            {
                return Unauthorized("Vui lòng đăng nhập");
            }

            var now = DateTimeOffset.UtcNow;
            var updated = await _context.UserNotifications
                .Where(n => n.UserId == user.Id && !n.IsRead)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, now));

            return Ok(new { message = "Đã đánh dấu tất cả thông báo", updated });
        }
    }
}
