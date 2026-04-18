using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartHelpdesk.Common.Identity;
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
        private readonly IFileService _fileService;
        private IValidator<CreateCommentDTO> _createCommentValidator;
        private IValidator<CreateTicketDTO> _createTicketValidator;
        private IValidator<UpdateTicketDTO> _updateTicketValidator;

        public TicketsController(
            ITicketsService ticketsService,
            UserManager<User> userManager,
            ICommentsService commentsService,
            IFileService fileService,
            IValidator<CreateTicketDTO> createTicketValidator,
            IValidator<UpdateTicketDTO> updateTicketValidator,
            IValidator<CreateCommentDTO> createCommentValidator)
        {
            _ticketsService = ticketsService;
            _userManager = userManager;
            _commentsService = commentsService;
            _fileService = fileService;
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
            var user = await _userManager.GetCurrentUserAsync(User);
            
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
            
            var user = await _userManager.GetCurrentUserAsync(User);
            
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
                EmailFromToken = user?.Email ?? User.FindFirst(ClaimTypes.Email)?.Value,
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

        // Endpoint cho Admin và Agent - lấy tất cả tickets (không filter theo userId)
        [HttpGet("GetAllTickets")]
        [Authorize]
        public async Task<IActionResult> GetAllTickets([FromQuery] int Take = 1000, [FromQuery] int Skip = 0)
        {
            try
            {
                var tickets = await _ticketsService.GetTicketsRaw(Take, Skip, userId: null);
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
                var user = await _userManager.GetCurrentUserAsync(User);
                
                if (user == null)
                {
                    return Unauthorized("Vui lòng đăng nhập");
                }
                
                Console.WriteLine($"DEBUG GetMyTickets: userId = {user.Id}, email = {user.Email}");
                
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

                var user = await _userManager.GetCurrentUserAsync(User);
                
                if (user == null)
                {
                    Console.WriteLine($"DEBUG TicketDetails: User not found from claims");
                    return Unauthorized("Vui lòng đăng nhập");
                }
                
                Console.WriteLine($"DEBUG TicketDetails: currentUserEmail = {user.Email}");
                
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
            var user = await _userManager.GetCurrentUserAsync(User);
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

        [HttpPost("{id}/feedback")]
        [Authorize]
        public async Task<IActionResult> SubmitTicketFeedback(Guid id, [FromBody] SubmitTicketFeedbackDTO dto)
        {
            try
            {
                var user = await _userManager.GetCurrentUserAsync(User);
                if (user == null)
                    return Unauthorized("Vui lòng đăng nhập");

                var feedback = await _ticketsService.SubmitTicketFeedback(id, user.Id, dto);
                return Ok(feedback);
            }
            catch (NotFoundException)
            {
                return NotFound("Không tìm thấy ticket");
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id}/feedback")]
        [Authorize]
        public async Task<IActionResult> GetTicketFeedback(Guid id)
        {
            var feedback = await _ticketsService.GetTicketFeedback(id);
            if (feedback == null)
                return NotFound();

            return Ok(feedback);
        }

        [HttpPost("CreateTicketWithAttachments")]
        [Authorize]
        [RequestSizeLimit(50 * 1024 * 1024)] // 50MB max
        [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
        public async Task<IActionResult> CreateTicketWithAttachments(
            [FromForm] string Description,
            [FromForm] string? ProductName,
            [FromForm] int Priority,
            [FromForm] IFormFileCollection files)
        {
            try
            {
                Console.WriteLine($"DEBUG CreateTicketWithAttachments: Description={Description?.Substring(0, Math.Min(30, Description?.Length ?? 0))}, files={files?.Count ?? 0}");

                var user = await _userManager.GetCurrentUserAsync(User);
                if (user == null)
                {
                    return Unauthorized("Vui lòng đăng nhập để gửi yêu cầu");
                }

                var ticketDTO = new CreateTicketDTO
                {
                    Description = Description,
                    ProductName = ProductName,
                    Priority = (SmartHelpdesk.Data.Enums.Priority)Priority,
                    UserId = user.Id
                };

                var validationRes = _createTicketValidator.Validate(ticketDTO);
                if (!validationRes.IsValid)
                    return BadRequest(validationRes);

                var ticketId = await _ticketsService.CreateTicket(ticketDTO);
                Console.WriteLine($"DEBUG CreateTicketWithAttachments: ticketId={ticketId}");

                // Nếu có file thì tạo 1 comment đầu tiên để gắn attachments theo CommentId.
                if (files != null && files.Count > 0)
                {
                    var commentText = "Khách hàng gửi yêu cầu kèm file đính kèm.";

                    var commentDTO = new CreateCommentDTO
                    {
                        TicketId = ticketId,
                        UserId = user.Id,
                        Text = commentText
                    };

                    var commentValidationRes = _createCommentValidator.Validate(commentDTO);
                    if (!commentValidationRes.IsValid)
                        return BadRequest(commentValidationRes);

                    var commentId = await _commentsService.CreateComment(commentDTO);
                    Console.WriteLine($"DEBUG CreateTicketWithAttachments: commentId={commentId}");

                    foreach (var file in files)
                    {
                        if (file == null || file.Length == 0) continue;
                        Console.WriteLine($"DEBUG: Saving attachment: {file.FileName}, size={file.Length}");
                        await _fileService.SaveAttachment(file, commentId);
                        Console.WriteLine($"DEBUG: Attachment saved: {file.FileName}");
                    }
                }

                Console.WriteLine($"DEBUG CreateTicketWithAttachments: Done, returning ticketId={ticketId}");
                return Ok(ticketId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR CreateTicketWithAttachments: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                return StatusCode(500, new { error = ex.Message, detail = ex.StackTrace });
            }
        }

        [HttpPut("UpdateTicket/{id}")]
        public async Task<IActionResult> UpdateTicket(Guid id, UpdateTicketDTO ticketDTO)
        {
            var validationRes = _updateTicketValidator.Validate(ticketDTO);
            if (!validationRes.IsValid)
                return BadRequest(validationRes);
            try
            {
                var user = await _userManager.GetCurrentUserAsync(User);
                if (user == null)
                    return Unauthorized("Vui lòng đăng nhập");

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
        [Authorize(Roles = "Admin,Agent,Nhân viên")]
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
            var user = await _userManager.GetCurrentUserAsync(User);
            if (user == null)
                return Unauthorized("Vui lòng đăng nhập");

            // Always trust identity from JWT, not client payload.
            commentDTO.UserId = user.Id;

            var validationRes = _createCommentValidator.Validate(commentDTO);
            if (!validationRes.IsValid)
                return BadRequest(validationRes);

            try
            {
                var ticket = await _ticketsService.GetTicket(commentDTO.TicketId);

                var isCustomer = await _userManager.IsInRoleAsync(user, "Customer")
                    || await _userManager.IsInRoleAsync(user, "Khách hàng");

                if (isCustomer && ticket.UserId != user.Id)
                {
                    return Forbid();
                }
            }
            catch (NotFoundException)
            {
                return NotFound("Không tìm thấy ticket");
            }

            var commentId = await _commentsService.CreateComment(commentDTO);

            return Ok(commentId);
        }

        [HttpGet("Comments/GetCommentsToTicket/{ticketId}")]
        public async Task<IActionResult> GetCommentsToTicket(Guid ticketId)
        {

            var user = await _userManager.GetCurrentUserAsync(User);
            if (user == null)
                return Unauthorized("Vui lòng đăng nhập");

            try
            {
                var ticket = await _ticketsService.GetTicket(ticketId);

                var isCustomer = await _userManager.IsInRoleAsync(user, "Customer")
                    || await _userManager.IsInRoleAsync(user, "Khách hàng");

                if (isCustomer && ticket.UserId != user.Id)
                {
                    return Forbid();
                }

                var comments = await _commentsService.GetCommentsToTicket(ticketId);
                return Ok(comments);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }

        }

        // ============================================================
        // ASSIGN TICKET APIs
        // ============================================================

        /// <summary>
        /// Admin gán ticket cho một nhân viên cụ thể (hoặc gỡ gán nếu AgentId = null)
        /// </summary>
        [HttpPost("AssignTicket/{id}")]
        [Authorize(Roles = "Admin,Quản trị viên")]
        public async Task<IActionResult> AssignTicket(Guid id, [FromBody] AssignTicketDTO dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest("Dữ liệu gán ticket không hợp lệ");
                }

                if (dto.AgentId.HasValue)
                {
                    var assignee = await _userManager.FindByIdAsync(dto.AgentId.Value.ToString());
                    if (assignee == null)
                    {
                        return BadRequest("Không tìm thấy nhân viên được chọn");
                    }

                    var isAgent = await _userManager.IsInRoleAsync(assignee, "Agent")
                        || await _userManager.IsInRoleAsync(assignee, "Nhân viên");

                    if (!isAgent)
                    {
                        return BadRequest("Người dùng được chọn không thuộc nhóm nhân viên hỗ trợ");
                    }
                }

                await _ticketsService.AssignTicket(id, dto.AgentId);
                return Ok(new { message = dto.AgentId.HasValue ? "Đã gán nhân viên thành công" : "Đã hủy gán nhân viên" });
            }
            catch (NotFoundException)
            {
                return NotFound("Không tìm thấy ticket");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Nhân viên tự nhận ticket (self-assign)
        /// </summary>
        [HttpPost("SelfAssign/{id}")]
        [Authorize(Roles = "Admin,Agent,Quản trị viên,Nhân viên")]
        public async Task<IActionResult> SelfAssign(Guid id)
        {
            try
            {
                var user = await _userManager.GetCurrentUserAsync(User);
                if (user == null) return Unauthorized();

                var ticket = await _ticketsService.GetTicket(id);

                if (ticket.AssignedToId.HasValue)
                {
                    if (ticket.AssignedToId.Value == user.Id)
                    {
                        return Ok(new { message = "Ticket này đã được bạn nhận trước đó", agentName = user.Name + " " + user.Surname });
                    }

                    return Conflict("Ticket đã có nhân viên khác phụ trách");
                }

                await _ticketsService.AssignTicket(id, user.Id);
                return Ok(new { message = "Đã nhận ticket thành công", agentName = user.Name + " " + user.Surname });
            }
            catch (NotFoundException)
            {
                return NotFound("Không tìm thấy ticket");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy các ticket đang được gán cho nhân viên hiện tại
        /// </summary>
        [HttpGet("MyAssignedTickets")]
        [Authorize(Roles = "Admin,Agent,Quản trị viên,Nhân viên")]
        public async Task<IActionResult> GetMyAssignedTickets([FromQuery] int Take = 1000, [FromQuery] int Skip = 0)
        {
            try
            {
                var user = await _userManager.GetCurrentUserAsync(User);
                if (user == null) return Unauthorized();

                var tickets = await _ticketsService.GetAgentSmartQueue(user.Id, new AgentTicketFiltersDTO
                {
                    Take = Take,
                    Skip = Skip
                });

                return Ok(tickets);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy các ticket chưa được gán cho ai (Unassigned)
        /// </summary>
        [HttpGet("UnassignedTickets")]
        [Authorize(Roles = "Admin,Agent,Quản trị viên,Nhân viên")]
        public async Task<IActionResult> GetUnassignedTickets([FromQuery] int Take = 1000, [FromQuery] int Skip = 0)
        {
            try
            {
                var tickets = await _ticketsService.GetUnassignedTickets(new AgentTicketFiltersDTO
                {
                    Take = Take,
                    Skip = Skip
                });

                return Ok(tickets);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thống kê hiệu suất của một nhân viên cụ thể
        /// </summary>
        [HttpGet("AgentStats/{agentId}")]
        [Authorize(Roles = "Admin,Agent")]
        public async Task<IActionResult> GetAgentStats(Guid agentId)
        {
            try
            {
                var stats = await _ticketsService.GetAgentStats(agentId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thống kê hiệu suất của nhân viên hiện tại (self)
        /// </summary>
        [HttpGet("MyStats")]
        [Authorize(Roles = "Admin,Agent")]
        public async Task<IActionResult> GetMyStats()
        {
            try
            {
                var user = await _userManager.GetCurrentUserAsync(User);
                if (user == null) return Unauthorized();

                var stats = await _ticketsService.GetAgentStats(user.Id);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ✅ AGENT DASHBOARD ENDPOINTS ✅

        /// <summary>
        /// Danh sách Smart Queue cho agent (Tickets đã gán + sắp xếp theo Priority + waiting time)
        /// </summary>
        [HttpGet("AgentDashboard/SmartQueue")]
        [Authorize(Roles = "Admin,Agent")]
        public async Task<IActionResult> GetAgentSmartQueue([FromQuery] AgentTicketFiltersDTO filters)
        {
            try
            {
                var user = await _userManager.GetCurrentUserAsync(User);
                if (user == null) return Unauthorized();

                var queue = await _ticketsService.GetAgentSmartQueue(user.Id, filters);
                return Ok(queue);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Danh sách Unassigned Tickets (Tickets chưa được gán - agent có thể pick/assign cho mình)
        /// </summary>
        [HttpGet("AgentDashboard/UnassignedQueue")]
        [Authorize(Roles = "Admin,Agent")]
        public async Task<IActionResult> GetUnassignedQueue([FromQuery] AgentTicketFiltersDTO filters)
        {
            try
            {
                var queue = await _ticketsService.GetUnassignedTickets(filters);
                return Ok(queue);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ✅ ADMIN DASHBOARD ENDPOINTS ✅

        /// <summary>
        /// Dashboard chính cho Admin - Tất cả metrics (tickets, sentiment, agent stats, trends...)
        /// </summary>
        [HttpGet("AdminDashboard")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAdminDashboard([FromQuery] int days = 30, [FromQuery] Guid? agentId = null)
        {
            try
            {
                var dashboard = await _ticketsService.GetAdminDashboard(days, agentId);
                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
