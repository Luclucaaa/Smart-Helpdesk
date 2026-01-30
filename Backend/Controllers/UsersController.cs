using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHelpdesk.Data;
using SmartHelpdesk.Data.Entities;

namespace SmartHelpdesk.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly SmartHelpdeskContext _context;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;

        public UsersController(SmartHelpdeskContext context, UserManager<User> userManager, RoleManager<Role> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        /// <summary>
        /// Lấy danh sách tất cả người dùng
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .Select(u => new UserDetailDTO
                {
                    Id = u.Id,
                    Name = u.Name,
                    Surname = u.Surname,
                    Email = u.Email ?? "",
                    CreatedTicketsCount = u.CreatedTickets.Count,
                    AssignedTicketsCount = u.AssignedTickets.Count
                })
                .ToListAsync();

            // Lấy role cho mỗi user
            foreach (var user in users)
            {
                var appUser = await _userManager.FindByIdAsync(user.Id.ToString());
                if (appUser != null)
                {
                    var roles = await _userManager.GetRolesAsync(appUser);
                    user.Role = roles.FirstOrDefault() ?? "Customer";
                }
            }

            return Ok(users);
        }

        /// <summary>
        /// Lấy thông tin chi tiết một người dùng
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUser(Guid id)
        {
            var user = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new UserDetailDTO
                {
                    Id = u.Id,
                    Name = u.Name,
                    Surname = u.Surname,
                    Email = u.Email ?? "",
                    CreatedTicketsCount = u.CreatedTickets.Count,
                    AssignedTicketsCount = u.AssignedTickets.Count
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound();

            var appUser = await _userManager.FindByIdAsync(user.Id.ToString());
            if (appUser != null)
            {
                var roles = await _userManager.GetRolesAsync(appUser);
                user.Role = roles.FirstOrDefault() ?? "Customer";
            }

            return Ok(user);
        }

        /// <summary>
        /// Cập nhật vai trò người dùng
        /// </summary>
        [HttpPut("{id}/role")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateUserRole(Guid id, [FromBody] UpdateRoleDTO dto)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound("Không tìm thấy người dùng");

            // Kiểm tra role hợp lệ
            var validRoles = new[] { "Admin", "Agent", "Customer" };
            if (!validRoles.Contains(dto.Role))
                return BadRequest("Vai trò không hợp lệ");

            // Xóa tất cả role hiện tại
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            // Thêm role mới
            var result = await _userManager.AddToRoleAsync(user, dto.Role);
            if (!result.Succeeded)
                return BadRequest("Không thể cập nhật vai trò");

            return Ok(new { message = "Cập nhật vai trò thành công" });
        }

        /// <summary>
        /// Lấy danh sách nhân viên hỗ trợ (Agent)
        /// </summary>
        [HttpGet("agents")]
        [Authorize(Roles = "Admin,Agent")]
        public async Task<IActionResult> GetAgents()
        {
            var agentRole = await _roleManager.FindByNameAsync("Agent");
            if (agentRole == null)
                return Ok(new List<UserDetailDTO>());

            var agentUserIds = await _context.UserRoles
                .Where(ur => ur.RoleId == agentRole.Id)
                .Select(ur => ur.UserId)
                .ToListAsync();

            var agents = await _context.Users
                .Where(u => agentUserIds.Contains(u.Id))
                .Select(u => new UserDetailDTO
                {
                    Id = u.Id,
                    Name = u.Name,
                    Surname = u.Surname,
                    Email = u.Email ?? "",
                    Role = "Agent",
                    AssignedTicketsCount = u.AssignedTickets.Count
                })
                .ToListAsync();

            return Ok(agents);
        }

        /// <summary>
        /// Thống kê người dùng theo vai trò
        /// </summary>
        [HttpGet("stats")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUserStats()
        {
            var adminRole = await _roleManager.FindByNameAsync("Admin");
            var agentRole = await _roleManager.FindByNameAsync("Agent");
            var customerRole = await _roleManager.FindByNameAsync("Customer");

            var adminCount = adminRole != null 
                ? await _context.UserRoles.CountAsync(ur => ur.RoleId == adminRole.Id) 
                : 0;
            var agentCount = agentRole != null 
                ? await _context.UserRoles.CountAsync(ur => ur.RoleId == agentRole.Id) 
                : 0;
            var customerCount = customerRole != null 
                ? await _context.UserRoles.CountAsync(ur => ur.RoleId == customerRole.Id) 
                : 0;

            return Ok(new
            {
                TotalUsers = await _context.Users.CountAsync(),
                AdminCount = adminCount,
                AgentCount = agentCount,
                CustomerCount = customerCount
            });
        }
    }

    // DTOs
    public class UserDetailDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Surname { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "Customer";
        public int CreatedTicketsCount { get; set; }
        public int AssignedTicketsCount { get; set; }
    }

    public class UpdateRoleDTO
    {
        public string Role { get; set; } = "Customer";
    }
}
