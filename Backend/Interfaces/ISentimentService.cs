using SmartHelpdesk.DTOs.Requests;
using SmartHelpdesk.DTOs.Responses;

namespace SmartHelpdesk.Interfaces;

/// <summary>
/// Service phân tích cảm xúc sử dụng ML.NET
/// </summary>
public interface ISentimentService
{
    /// <summary>
    /// Phân tích cảm xúc từ văn bản
    /// </summary>
    /// <param name="text">Văn bản cần phân tích</param>
    /// <returns>Kết quả phân tích cảm xúc</returns>
    SentimentResponse AnalyzeSentiment(string text);
    
    /// <summary>
    /// Phân tích cảm xúc từ request
    /// </summary>
    SentimentResponse AnalyzeSentiment(SentimentRequest request);
}
