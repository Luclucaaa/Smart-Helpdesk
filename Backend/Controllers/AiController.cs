using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartHelpdesk.Data.Entities;
using Microsoft.AspNetCore.Identity;
using SmartHelpdesk.Common.Identity;
using SmartHelpdesk.DTOs.Requests;
using SmartHelpdesk.DTOs.Responses;
using SmartHelpdesk.Interfaces;
using SmartHelpdesk.Services;

namespace SmartHelpdesk.Controllers;

/// <summary>
/// API Controller cho các tính năng AI
/// </summary>
[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly ISentimentService _sentimentService;
    private readonly ICategoryClassifierService _categoryClassifierService;
    private readonly ITicketsService _ticketsService;
    private readonly ICannedResponsesService _cannedResponsesService;
    private readonly IAiSuggestionLogService _aiSuggestionLogService;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<AiController> _logger;
    private readonly GeminiService _geminiService;

    public AiController(
        ISentimentService sentimentService,
        ICategoryClassifierService categoryClassifierService,
        ITicketsService ticketsService,
        ICannedResponsesService cannedResponsesService,
        IAiSuggestionLogService aiSuggestionLogService,
        UserManager<User> userManager,
        ILogger<AiController> logger,
        GeminiService geminiService)
    {
        _sentimentService = sentimentService;
        _categoryClassifierService = categoryClassifierService;
        _ticketsService = ticketsService;
        _cannedResponsesService = cannedResponsesService;
        _aiSuggestionLogService = aiSuggestionLogService;
        _userManager = userManager;
        _logger = logger;
        _geminiService = geminiService;
    }

    /// <summary>
    /// Phân tích cảm xúc từ văn bản
    /// </summary>
    /// <param name="request">Request chứa văn bản cần phân tích</param>
    /// <returns>Kết quả phân tích cảm xúc</returns>
    /// <response code="200">Phân tích thành công</response>
    /// <response code="400">Request không hợp lệ</response>
    /// <response code="500">Lỗi server</response>
    [HttpPost("sentiment")]
    [ProducesResponseType(typeof(SentimentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<SentimentResponse> AnalyzeSentiment([FromBody] SentimentRequest request)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { error = "Text is required" });
        }

        if (request.Text.Length > 5000)
        {
            return BadRequest(new { error = "Text is too long. Maximum 5000 characters." });
        }

        try
        {
            var result = _sentimentService.AnalyzeSentiment(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AnalyzeSentiment endpoint");
            return StatusCode(500, new { error = "Failed to analyze sentiment" });
        }
    }

    /// <summary>
    /// Phân tích cảm xúc nhanh (GET method)
    /// </summary>
    /// <param name="text">Văn bản cần phân tích</param>
    [HttpGet("sentiment")]
    [ProducesResponseType(typeof(SentimentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<SentimentResponse> AnalyzeSentimentQuick([FromQuery] string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest(new { error = "Text query parameter is required" });
        }

        try
        {
            var result = _sentimentService.AnalyzeSentiment(text);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AnalyzeSentimentQuick endpoint");
            return StatusCode(500, new { error = "Failed to analyze sentiment" });
        }
    }

    [HttpPost("classify-category")]
    [ProducesResponseType(typeof(CategoryClassificationDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<CategoryClassificationDTO> ClassifyCategory([FromBody] CategoryClassificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new { error = "Description is required" });
        }

        var result = _categoryClassifierService.Classify(request.Description, request.Title, request.ProductName);
        return Ok(result);
    }

    /// <summary>
    /// Goi y noi dung nhap lieu ticket cho khach hang
    /// </summary>
    [HttpPost("suggest-input")]
    [ProducesResponseType(typeof(InputSuggestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InputSuggestionResponse>> SuggestInput([FromBody] InputSuggestionRequest request)
    {
        if (request.MaxSuggestions < 1 || request.MaxSuggestions > 5)
        {
            return BadRequest(new { error = "MaxSuggestions must be between 1 and 5" });
        }

        try
        {
            var (suggestions, source) = await _geminiService.SuggestTicketInputAsync(
                request.Description,
                request.ProductName,
                request.MaxSuggestions);

            return Ok(new InputSuggestionResponse
            {
                Suggestions = suggestions,
                Source = source
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SuggestInput endpoint");
            return StatusCode(500, new { error = "Failed to generate input suggestions" });
        }
    }

    /// <summary>
    /// Goi y cau tra loi cho Agent dua tren ngu canh ticket va lich su hoi thoai
    /// </summary>
    [HttpPost("suggest-reply")]
    [Authorize(Roles = "Admin,Agent,Quản trị viên,Nhân viên")]
    [ProducesResponseType(typeof(AgentReplySuggestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AgentReplySuggestionResponse>> SuggestReply([FromBody] AgentReplySuggestionRequest request)
    {
        if (request.TicketId == Guid.Empty)
        {
            return BadRequest(new { error = "TicketId is required" });
        }

        if (request.MaxSuggestions < 1 || request.MaxSuggestions > 5)
        {
            return BadRequest(new { error = "MaxSuggestions must be between 1 and 5" });
        }

        try
        {
            var user = await _userManager.GetCurrentUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { error = "Unauthorized" });
            }

            var ticket = await _ticketsService.GetTicket(request.TicketId);
            var recentConversation = ticket.Comments
                .OrderByDescending(c => c.CreatedAt)
                .Take(6)
                .OrderBy(c => c.CreatedAt)
                .Select(c => $"{(c.IsFromAgent ? "Agent" : "Customer")}: {c.Text}")
                .ToList();

            List<CannedResponseDTO> canned;
            if (ticket.Category.HasValue)
            {
                canned = await _cannedResponsesService.GetCannedResponsesByCategory(ticket.Category.Value.ToString());
            }
            else
            {
                canned = await _cannedResponsesService.GetAllCannedResponses();
            }

            var cannedTexts = canned
                .Where(c => !string.IsNullOrWhiteSpace(c.Text))
                .Take(6)
                .Select(c => c.Text)
                .ToList();

            var (suggestions, source) = await _geminiService.SuggestAgentRepliesAsync(
                ticketDescription: ticket.Description,
                productName: ticket.ProductName,
                category: ticket.Category?.ToString(),
                sentimentLabel: ticket.SentimentLabel,
                sentimentScore: ticket.SentimentScore,
                recentConversation: recentConversation,
                cannedResponses: cannedTexts,
                draftReply: request.DraftReply,
                maxSuggestions: request.MaxSuggestions);

            var logIds = await _aiSuggestionLogService.LogSuggestionsAsync(
                request.TicketId,
                user.Id,
                suggestions);

            for (var i = 0; i < suggestions.Count && i < logIds.Count; i++)
            {
                suggestions[i].SuggestionLogId = logIds[i];
            }

            return Ok(new AgentReplySuggestionResponse
            {
                TicketId = request.TicketId,
                Source = source,
                Suggestions = suggestions
            });
        }
        catch (Common.Exceptions.NotFoundException)
        {
            return NotFound(new { error = "Ticket not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SuggestReply endpoint for ticket {TicketId}", request.TicketId);
            return StatusCode(500, new { error = "Failed to generate reply suggestions" });
        }
    }

    /// <summary>
    /// Danh dau goi y AI duoc agent chon de su dung
    /// </summary>
    [HttpPost("suggest-reply/{suggestionLogId:guid}/accept")]
    [Authorize(Roles = "Admin,Agent,Quản trị viên,Nhân viên")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptReplySuggestion(Guid suggestionLogId)
    {
        var user = await _userManager.GetCurrentUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new { error = "Unauthorized" });
        }

        var updated = await _aiSuggestionLogService.MarkAcceptedAsync(suggestionLogId, user.Id);
        if (!updated)
        {
            return NotFound(new { error = "Suggestion log not found" });
        }

        return Ok(new { status = "accepted" });
    }

    /// <summary>
    /// Luu feedback huu ich/khong huu ich cho goi y AI
    /// </summary>
    [HttpPost("suggest-reply/{suggestionLogId:guid}/feedback")]
    [Authorize(Roles = "Admin,Agent,Quản trị viên,Nhân viên")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplySuggestionFeedback(
        Guid suggestionLogId,
        [FromBody] SuggestionFeedbackRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Note) && request.Note.Length > 500)
        {
            return BadRequest(new { error = "Feedback note must be <= 500 characters" });
        }

        var user = await _userManager.GetCurrentUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new { error = "Unauthorized" });
        }

        var updated = await _aiSuggestionLogService.SetFeedbackAsync(
            suggestionLogId,
            user.Id,
            request.IsHelpful,
            request.Note);

        if (!updated)
        {
            return NotFound(new { error = "Suggestion log not found" });
        }

        return Ok(new { status = "feedback_saved", helpful = request.IsHelpful });
    }

    /// <summary>
    /// Gửi câu hỏi tới Gemini AI và nhận phản hồi
    /// </summary>
    /// <param name="question">Câu hỏi từ người dùng</param>
    /// <returns>Phản hồi từ Gemini AI</returns>
    [HttpPost("ask")]
    public async Task<IActionResult> AskGemini([FromBody] string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return BadRequest(new { error = "Question is required" });

        if (question.Length > 1000)
            return BadRequest(new { error = "Question is too long. Maximum 1000 characters." });

        try
        {
            var answer = await _geminiService.AskGeminiAsync(question.Trim());
            return Ok(new { answer });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AskGemini endpoint");
            return StatusCode(500, new { error = "Failed to get answer from Gemini" });
        }
    }
}

public class CategoryClassificationRequest
{
    public string? Title { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ProductName { get; set; }
}
