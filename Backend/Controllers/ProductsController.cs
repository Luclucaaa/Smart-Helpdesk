using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHelpdesk.Common.Identity;
using SmartHelpdesk.Data;
using SmartHelpdesk.Data.Entities;
using SmartHelpdesk.DTOs.Requests;

namespace SmartHelpdesk.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly SmartHelpdeskContext _context;
        private readonly UserManager<User> _userManager;

        public ProductsController(SmartHelpdeskContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// <summary>
        /// Lấy danh sách tất cả sản phẩm
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetProducts([FromQuery] Guid? categoryId = null)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .AsQueryable();
                
            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }
            
            var products = await query
                .Select(p => new ProductDTO
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt,
                    TicketCount = p.Tickets.Count,
                    AssignedAgentCount = p.AgentAssignments.Count(a => a.IsActive),
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null
                })
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return Ok(products);
        }

        /// <summary>
        /// Lấy thông tin chi tiết một sản phẩm
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(Guid id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.Id == id)
                .Select(p => new ProductDTO
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt,
                    TicketCount = p.Tickets.Count,
                    AssignedAgentCount = p.AgentAssignments.Count(a => a.IsActive),
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null
                })
                .FirstOrDefaultAsync();

            if (product == null)
                return NotFound();

            return Ok(product);
        }

        /// <summary>
        /// Tạo sản phẩm mới
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Tên sản phẩm không được để trống");

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                CategoryId = dto.CategoryId,
                IsActive = dto.IsActive,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return Ok(new { id = product.Id, message = "Tạo sản phẩm thành công" });
        }

        /// <summary>
        /// Cập nhật sản phẩm
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductDTO dto)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Tên sản phẩm không được để trống");

            product.Name = dto.Name.Trim();
            product.Description = dto.Description?.Trim();
            product.CategoryId = dto.CategoryId;
            product.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Cập nhật sản phẩm thành công" });
        }

        /// <summary>
        /// Xóa sản phẩm
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            var product = await _context.Products
                .Include(p => p.Tickets)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            if (product.Tickets.Any())
                return BadRequest("Không thể xóa sản phẩm đang có ticket liên quan");

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa sản phẩm thành công" });
        }

        /// <summary>
        /// Lấy danh sách sản phẩm đang hoạt động (cho dropdown)
        /// </summary>
        [HttpGet("active")]
        [AllowAnonymous]
        public async Task<IActionResult> GetActiveProducts()
        {
            var products = await _context.Products
                .Where(p => p.IsActive)
                .Select(p => new { p.Id, p.Name })
                .OrderBy(p => p.Name)
                .ToListAsync();

            return Ok(products);
        }

        [HttpGet("{id}/assignments")]
        [Authorize(Roles = "Admin,Agent,Quản trị viên,Nhân viên")]
        public async Task<IActionResult> GetProductAssignments(Guid id)
        {
            var productExists = await _context.Products.AnyAsync(p => p.Id == id);
            if (!productExists)
            {
                return NotFound("Không tìm thấy sản phẩm");
            }

            var assignments = await _context.ProductAgentAssignments
                .Where(x => x.ProductId == id && x.IsActive)
                .Select(x => new ProductAgentAssignmentDTO
                {
                    AssignmentId = x.Id,
                    ProductId = x.ProductId,
                    AgentId = x.AgentId,
                    AgentName = x.Agent.Name,
                    AgentSurname = x.Agent.Surname,
                    AgentEmail = x.Agent.Email ?? string.Empty,
                    CreatedAt = x.CreatedAt
                })
                .OrderBy(x => x.AgentName)
                .ToListAsync();

            return Ok(assignments);
        }

        [HttpPut("{id}/assignments")]
        [Authorize(Roles = "Admin,Quản trị viên")]
        public async Task<IActionResult> UpdateProductAssignments(Guid id, [FromBody] UpdateProductAssignmentsDTO dto)
        {
            var productExists = await _context.Products.AnyAsync(p => p.Id == id);
            if (!productExists)
            {
                return NotFound("Không tìm thấy sản phẩm");
            }

            var desiredIds = (dto.AgentIds ?? new List<Guid>()).Distinct().ToList();

            foreach (var agentId in desiredIds)
            {
                var user = await _userManager.FindByIdAsync(agentId.ToString());
                if (user == null)
                {
                    return BadRequest($"Không tìm thấy nhân viên: {agentId}");
                }

                var isAgent = await _userManager.IsInRoleAsync(user, "Agent")
                    || await _userManager.IsInRoleAsync(user, "Nhân viên");

                if (!isAgent)
                {
                    return BadRequest($"Người dùng {user.Email} không thuộc nhóm nhân viên hỗ trợ");
                }
            }

            var currentAssignments = await _context.ProductAgentAssignments
                .Where(x => x.ProductId == id)
                .ToListAsync();

            var now = DateTimeOffset.UtcNow;

            foreach (var assignment in currentAssignments)
            {
                if (!desiredIds.Contains(assignment.AgentId))
                {
                    assignment.IsActive = false;
                    assignment.UpdatedAt = now;
                }
            }

            foreach (var agentId in desiredIds)
            {
                var existing = currentAssignments.FirstOrDefault(x => x.AgentId == agentId);
                if (existing == null)
                {
                    _context.ProductAgentAssignments.Add(new ProductAgentAssignment
                    {
                        Id = Guid.NewGuid(),
                        ProductId = id,
                        AgentId = agentId,
                        IsActive = true,
                        CreatedAt = now
                    });
                }
                else if (!existing.IsActive)
                {
                    existing.IsActive = true;
                    existing.UpdatedAt = now;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật phân công sản phẩm thành công", totalAgents = desiredIds.Count });
        }
    }

    // DTOs
    public class ProductDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public int TicketCount { get; set; }
        public int AssignedAgentCount { get; set; }
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
    }

    public class ProductAgentAssignmentDTO
    {
        public Guid AssignmentId { get; set; }
        public Guid ProductId { get; set; }
        public Guid AgentId { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public string AgentSurname { get; set; } = string.Empty;
        public string AgentEmail { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class CreateProductDTO
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public Guid? CategoryId { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateProductDTO
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public Guid? CategoryId { get; set; }
        public bool IsActive { get; set; }
    }
}
