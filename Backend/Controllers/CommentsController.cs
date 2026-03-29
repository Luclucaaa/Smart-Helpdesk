using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartHelpdesk.Common.Identity;
using SmartHelpdesk.Common.Exceptions;
using SmartHelpdesk.Data.Entities;
using SmartHelpdesk.DTOs.Requests;
using SmartHelpdesk.DTOs.Responses;
using SmartHelpdesk.Interfaces;
using SmartHelpdesk.Services;
using SmartHelpdesk.Validators;

namespace SmartHelpdesk.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CommentsController : ControllerBase
    {
        private readonly ICommentsService _commentsService;
        private readonly UserManager<User> _userManager;
        private IValidator<UpdateCommentDTO> _updateCommentValidator;

        public CommentsController(ICommentsService commentsService, UserManager<User> userManager, IValidator<UpdateCommentDTO> updateCommentValidator)
        {
            _commentsService = commentsService;
            _userManager = userManager;
            _updateCommentValidator = updateCommentValidator;
        }

        [HttpPut("UpdateComment/{id}")]
        public async Task<IActionResult> UpdateComment(Guid id, UpdateCommentDTO commentDTO)
        {
            var validationRes = _updateCommentValidator.Validate(commentDTO);
            if (!validationRes.IsValid)
                return BadRequest(validationRes);

            try
            {
                var user = await _userManager.GetCurrentUserAsync(User);
                if (user == null)
                    return Unauthorized("Vui lòng đăng nhập");

                var isCustomer = await _userManager.IsInRoleAsync(user, "Customer");

                if (isCustomer && user.Comments.FirstOrDefault(c => c.Id == id) == null)
                {
                    return Forbid();
                }

                await _commentsService.UpdateComment(id, commentDTO);

                return Ok();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("DeleteComment/{id}")]
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> DeleteComment(Guid id)
        {
            try
            {
                await _commentsService.DeleteComment(id);

                return Ok();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }
    }
}
