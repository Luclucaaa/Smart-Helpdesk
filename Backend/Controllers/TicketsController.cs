using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartHelpdesk.Common.Exceptions;
using SmartHelpdesk.Data.Entities;
using SmartHelpdesk.DTOs.Requests;
using SmartHelpdesk.Interfaces;
using System.Net.Sockets;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using SmartHelpdesk.Services;
using SmartHelpdesk.DTOs.Responses;
using System.Xml.Linq;
using FluentValidation;
using SmartHelpdesk.Validators;

namespace SmartHelpdesk.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TicketsController : ControllerBase
    {
        private readonly ITicketsService _ticketsService;
        private readonly UserManager<User> _userManager;
        private readonly ICommentsService _commentsService;
        private IValidator<CreateCommentDTO> _createCommentValidator;
        private IValidator<CreateTicketDTO> _createTicketValidator;
        private IValidator<UpdateTicketDTO> _updateTicketValidator;

        public TicketsController(ITicketsService ticketsService, UserManager<User> userManager, ICommentsService commentsService, IValidator<CreateTicketDTO> createTicketValidator, IValidator<UpdateTicketDTO> updateTicketValidator, IValidator<CreateCommentDTO> createCommentValidator)
        {
            _ticketsService = ticketsService;
            _userManager = userManager;
            _commentsService = commentsService;
            _createTicketValidator = createTicketValidator;
            _createCommentValidator = createCommentValidator;
            _updateTicketValidator = updateTicketValidator;
        }

        [HttpGet("GetTickets")]
        [Authorize(Roles = "Admin,Nhân viên")]
        public async Task<IActionResult> GetTickets([FromQuery]TicketsQueryFilters filters)
        {
            var tickets = await _ticketsService.GetTickets(filters);

            return Ok(tickets);
        }

        [HttpGet("TicketDetails/{id}")]
        public async Task<IActionResult> TicketDetails(Guid id)
        {
            try
            {
                var ticket = await _ticketsService.GetTicket(id);

                var currentUserEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                var user = await _userManager.FindByEmailAsync(currentUserEmail);
                var isCustomer = await _userManager.IsInRoleAsync(user, "Customer");

                if (isCustomer && ticket.UserId != user.Id)
                {
                    return Forbid();
                }

                return Ok(ticket);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPost("CreateTicket")]
        public async Task<IActionResult> CreateTicket(CreateTicketDTO ticketDTO)
        {
            var validationRes = _createTicketValidator.Validate(ticketDTO);
            if (!validationRes.IsValid)
                return BadRequest(validationRes);

            var ticketId = await _ticketsService.CreateTicket(ticketDTO);

            return Ok(ticketId);
        }

        [HttpPut("UpdateTicket/{id}")]
        public async Task<IActionResult> UpdateTicket(Guid id, UpdateTicketDTO ticketDTO)
        {
            var validationRes = _updateTicketValidator.Validate(ticketDTO);
            if (!validationRes.IsValid)
                return BadRequest(validationRes);
            try
            {
                var currentUserEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                var user = await _userManager.FindByEmailAsync(currentUserEmail);
                var isCustomer = await _userManager.IsInRoleAsync(user, "Customer");

                if (isCustomer && user.CreatedTickets.FirstOrDefault(t => t.Id == id) == null)
                {
                    return Forbid();
                }

                await _ticketsService.UpdateTicket(id, ticketDTO);

                return Ok();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
        }

        [HttpPatch("UpdateTicketStatus/{id}")]
        [Authorize(Roles = "Admin,Nhân viên")]
        public async Task<IActionResult> UpdateTicketStatus(Guid id, UpdateTicketStatusDTO statusDTO)
        {
            try
            {
                await _ticketsService.UpdateTicketStatus(id, statusDTO.Status);
                return Ok();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("DeleteTicket/{id}")]
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> DeleteTicket(Guid id)
        {
            try
            {
                await _ticketsService.DeleteTicket(id);

                return Ok();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }


        [HttpPost("Comments/AddCommentToTicket")]

        public async Task<IActionResult> CreateComment(CreateCommentDTO commentDTO)
        {
            var validationRes = _createCommentValidator.Validate(commentDTO);
            if (!validationRes.IsValid)
                return BadRequest(validationRes);

            var commentId = await _commentsService.CreateComment(commentDTO);

            return Ok(commentId);
        }

        [HttpGet("Comments/GetCommentsToTicket/{ticketId}")]
        public async Task<IActionResult> GetCommentsToTicket(Guid ticketId)
        {

            var currentUserEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var user = await _userManager.FindByEmailAsync(currentUserEmail);
            var isCustomer = await _userManager.IsInRoleAsync(user, "Customer");


            if (isCustomer && user.CreatedTickets.FirstOrDefault(t => t.Id == ticketId) == null)
            {
                return Forbid();
            }

            try
            {
               var comments  = await _commentsService.GetCommentsToTicket(ticketId);
               return Ok(comments);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }

        }
    }
}
