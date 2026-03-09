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

        [HttpGet("DebugAllTicketIds")]
        [AllowAnonymous]
        public async Task<IActionResult> DebugAllTicketIds()
        {
            // Endpoint debug - không cần auth
            var allTicketIds = await _ticketsService.GetAllTicketIdsForDebug();
            return Ok(allTicketIds);
        }

        [HttpGet("DebugTicketById/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> DebugTicketById(Guid id)
        {
            try
            {
                // Test GetTicket (the real method used by TicketDetails)
                var ticket = await _ticketsService.GetTicket(id);
                return Ok(new 
                {
                    Found = true,
                    TicketId = ticket.Id,
                    UserId = ticket.UserId,
                    Description = ticket.Description?.Substring(0, Math.Min(50, ticket.Description?.Length ?? 0)),
                    CommentsCount = ticket.Comments?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                return Ok(new 
                {
                    Found = false,
                    Error = ex.Message,
                    ErrorType = ex.GetType().Name,
                    StackTrace = ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0))
                });
            }
        }

        [HttpGet("DebugTicket/{id}")]
        [Authorize]
        public async Task<IActionResult> DebugTicket(Guid id)
        {
            var currentUserEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByEmailAsync(currentUserEmail);
            
            try
            {
                var ticket = await _ticketsService.GetTicket(id);
                return Ok(new 
                {
                    TicketFound = true,
                    TicketId = ticket.Id,
                    TicketUserId = ticket.UserId,
                    CurrentUserId = user?.Id,
                    IsOwner = ticket.UserId == user?.Id,
                    TicketDescription = ticket.Description?.Substring(0, Math.Min(50, ticket.Description?.Length ?? 0))
                });
            }
            catch (Exception ex)
            {
                return Ok(new 
                {
                    TicketFound = false,
                    Error = ex.Message,
                    CurrentUserId = user?.Id,
                    SearchedId = id
                });
            }
        }

        [HttpGet("DebugMyInfo")]
        [Authorize]
        public async Task<IActionResult> DebugMyInfo()
        {
            var allClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            var roles = User.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value).ToList();
            
            var currentUserEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst("sub")?.Value;
            
            var user = await _userManager.FindByEmailAsync(currentUserEmail);
            
            // Lấy tất cả tickets trong database
            var allTicketsCount = await _ticketsService.GetTickets(new TicketsQueryFilters { Take = 1000, Skip = 0 });
            
            // Lấy tickets của user này
            var myTickets = user != null 
                ? await _ticketsService.GetTickets(new TicketsQueryFilters { Take = 1000, Skip = 0, UserId = user.Id })
                : null;
            
            return Ok(new 
            {
                Claims = allClaims,
                Roles = roles,
                EmailFromToken = currentUserEmail,
                UserFound = user != null,
                UserId = user?.Id,
                UserEmail = user?.Email,
                UserName = user?.Name,
                AllTicketsTotal = allTicketsCount.Total,
                MyTicketsTotal = myTickets?.Total ?? 0,
                MyTicketsList = myTickets?.Tickets?.Select(t => new { t.Id, t.Description, t.UserId }).ToList()
            });
        }

        [HttpGet("GetTickets")]
        [Authorize(Roles = "Admin,Agent,Quản trị viên,Nhân viên")]
        public async Task<IActionResult> GetTickets([FromQuery]TicketsQueryFilters filters)
        {
            var tickets = await _ticketsService.GetTickets(filters);

            return Ok(tickets);
        }

        // Endpoint mới cho Admin - lấy tất cả tickets
        [HttpGet("GetAllTickets")]
        [Authorize]
        public async Task<IActionResult> GetAllTickets([FromQuery]TicketsQueryFilters filters)
        {
            try
            {
                var tickets = await _ticketsService.GetTickets(filters);
                return Ok(tickets);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR GetAllTickets: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("GetMyTickets")]
        [Authorize]
        public async Task<IActionResult> GetMyTickets([FromQuery]TicketsQueryFilters filters)
        {
            try
            {
                // Lấy userId từ token
                var currentUserEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var user = await _userManager.FindByEmailAsync(currentUserEmail);
                
                if (user == null)
                {
                    return Unauthorized("Vui lòng đăng nhập");
                }
                
                Console.WriteLine($"DEBUG GetMyTickets: userId = {user.Id}, email = {currentUserEmail}");
                
                // Chỉ lấy tickets của user hiện tại
                var tickets = await _ticketsService.GetTicketsRaw(filters.Take, filters.Skip, user.Id);
                return Ok(tickets);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR GetMyTickets: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
            }
        }

        [HttpGet("TicketDetails/{id}")]
        [Authorize]
        public async Task<IActionResult> TicketDetails(Guid id)
        {
            try
            {
                Console.WriteLine($"DEBUG TicketDetails: Looking for ticket {id}");
                
                var ticket = await _ticketsService.GetTicket(id);
                
                Console.WriteLine($"DEBUG TicketDetails: Found ticket, UserId = {ticket.UserId}");

                var currentUserEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                Console.WriteLine($"DEBUG TicketDetails: currentUserEmail = {currentUserEmail}");

                var user = await _userManager.FindByEmailAsync(currentUserEmail);
                
                if (user == null)
                {
                    Console.WriteLine($"DEBUG TicketDetails: User not found for email {currentUserEmail}");
                    return Unauthorized("Vui lòng đăng nhập");
                }
                
                Console.WriteLine($"DEBUG TicketDetails: Found user {user.Id}, checking roles...");
                
                // Kiểm tra cả "Customer" và "Khách hàng"
                var isCustomer = await _userManager.IsInRoleAsync(user, "Customer") 
                    || await _userManager.IsInRoleAsync(user, "Khách hàng");
                
                Console.WriteLine($"DEBUG TicketDetails: isCustomer = {isCustomer}, user.Id = {user.Id}, ticket.UserId = {ticket.UserId}");

                if (isCustomer && ticket.UserId != user.Id)
                {
                    Console.WriteLine($"DEBUG TicketDetails: Customer trying to access other's ticket - FORBIDDEN");
                    return Forbid();
                }

                Console.WriteLine($"DEBUG TicketDetails: Returning ticket OK");
                return Ok(ticket);
            }
            catch (NotFoundException)
            {
                Console.WriteLine($"DEBUG TicketDetails: Ticket {id} NOT FOUND in database");
                return NotFound();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TicketDetails: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("CreateTicket")]
        [Authorize]
        public async Task<IActionResult> CreateTicket(CreateTicketDTO ticketDTO)
        {
            // Tự động lấy UserId từ token
            var currentUserEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.FindByEmailAsync(currentUserEmail);
            if (user == null)
            {
                return Unauthorized("Vui lòng đăng nhập để gửi yêu cầu");
            }
            
            ticketDTO.UserId = user.Id;
            
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
