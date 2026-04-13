using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using SmartHelpdesk.Common.Identity;
using SmartHelpdesk.Data.Entities;
using SmartHelpdesk.DTOs.Requests;
using SmartHelpdesk.DTOs.Responses;
using SmartHelpdesk.Interfaces;
using System.Security.Claims;

namespace SmartHelpdesk.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CannedResponsesController : ControllerBase
    {
        private readonly ICannedResponsesService _cannedResponsesService;
        private readonly UserManager<User> _userManager;

        public CannedResponsesController(
            ICannedResponsesService cannedResponsesService,
            UserManager<User> userManager)
        {
            _cannedResponsesService = cannedResponsesService;
            _userManager = userManager;
        }

        /// <summary>
        /// Lấy tất cả Canned Responses (mẫu trả lời)
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllCannedResponses()
        {
            var responses = await _cannedResponsesService.GetAllCannedResponses();
            return Ok(responses);
        }

        /// <summary>
        /// Lấy Canned Responses theo Category (Bug/Feature/Sale/Support)
        /// </summary>
        [HttpGet("by-category/{category}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByCategory(string category)
        {
            var responses = await _cannedResponsesService.GetCannedResponsesByCategory(category);
            return Ok(responses);
        }

        /// <summary>
        /// Tạo Canned Response mới (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCannedResponse([FromBody] CreateCannedResponseDTO dto)
        {
            var user = await _userManager.GetCurrentUserAsync(User);
            dto.CreatedBy = user?.Id;

            var id = await _cannedResponsesService.CreateCannedResponse(dto);
            return Ok(new { id, message = "Canned response created successfully" });
        }

        /// <summary>
        /// Cập nhật Canned Response (Admin only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCannedResponse(Guid id, [FromBody] CreateCannedResponseDTO dto)
        {
            await _cannedResponsesService.UpdateCannedResponse(id, dto);
            return Ok(new { message = "Canned response updated successfully" });
        }

        /// <summary>
        /// Xóa Canned Response (Admin only - soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCannedResponse(Guid id)
        {
            await _cannedResponsesService.DeleteCannedResponse(id);
            return Ok(new { message = "Canned response deleted successfully" });
        }
    }
}
