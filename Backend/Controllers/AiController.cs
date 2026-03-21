using Microsoft.AspNetCore.Mvc;
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
    private readonly ILogger<AiController> _logger;
    private readonly GeminiService _geminiService;

    public AiController(
        ISentimentService sentimentService,
        ILogger<AiController> logger,
        GeminiService geminiService)
    {
        _sentimentService = sentimentService;
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
        try
        {
            var answer = await _geminiService.AskGeminiAsync(question);
            return Ok(new { answer });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AskGemini endpoint");
            return StatusCode(500, new { error = "Failed to get answer from Gemini" });
        }
    }
}
