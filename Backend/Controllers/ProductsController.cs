using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHelpdesk.Data;
using SmartHelpdesk.Data.Entities;

namespace SmartHelpdesk.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly SmartHelpdeskContext _context;

        public ProductsController(SmartHelpdeskContext context)
        {
            _context = context;
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
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
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
