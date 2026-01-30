using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHelpdesk.Data;
using SmartHelpdesk.Data.Entities;

namespace SmartHelpdesk.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductCategoriesController : ControllerBase
    {
        private readonly SmartHelpdeskContext _context;

        public ProductCategoriesController(SmartHelpdeskContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy danh sách tất cả loại sản phẩm
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.ProductCategories
                .OrderBy(c => c.Name)
                .Select(c => new ProductCategoryDTO
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    ProductCount = c.Products.Count
                })
                .ToListAsync();

            return Ok(categories);
        }

        /// <summary>
        /// Lấy danh sách loại sản phẩm đang hoạt động
        /// </summary>
        [HttpGet("active")]
        [AllowAnonymous]
        public async Task<IActionResult> GetActiveCategories()
        {
            var categories = await _context.ProductCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new ProductCategoryDTO
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    ProductCount = c.Products.Count(p => p.IsActive)
                })
                .ToListAsync();

            return Ok(categories);
        }

        /// <summary>
        /// Lấy chi tiết một loại sản phẩm
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategory(Guid id)
        {
            var category = await _context.ProductCategories
                .Where(c => c.Id == id)
                .Select(c => new ProductCategoryDTO
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    ProductCount = c.Products.Count
                })
                .FirstOrDefaultAsync();

            if (category == null)
                return NotFound();

            return Ok(category);
        }

        /// <summary>
        /// Tạo loại sản phẩm mới
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCategory([FromBody] CreateProductCategoryDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Tên loại sản phẩm không được để trống");

            var category = new ProductCategory
            {
                Id = Guid.NewGuid(),
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                IsActive = dto.IsActive,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.ProductCategories.Add(category);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, new ProductCategoryDTO
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                IsActive = category.IsActive,
                ProductCount = 0
            });
        }

        /// <summary>
        /// Cập nhật loại sản phẩm
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateProductCategoryDTO dto)
        {
            var category = await _context.ProductCategories.FindAsync(id);
            if (category == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Tên loại sản phẩm không được để trống");

            category.Name = dto.Name.Trim();
            category.Description = dto.Description?.Trim();
            category.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Cập nhật thành công" });
        }

        /// <summary>
        /// Xóa loại sản phẩm
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            var category = await _context.ProductCategories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return NotFound();

            // Gỡ liên kết với Products (set CategoryId = null)
            foreach (var product in category.Products)
            {
                product.CategoryId = null;
            }

            _context.ProductCategories.Remove(category);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa thành công" });
        }
    }

    // DTOs
    public class ProductCategoryDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int ProductCount { get; set; }
    }

    public class CreateProductCategoryDTO
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateProductCategoryDTO
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }
}
