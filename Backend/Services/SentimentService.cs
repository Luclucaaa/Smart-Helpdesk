using SmartHelpdesk.DTOs.Requests;
using SmartHelpdesk.DTOs.Responses;
using SmartHelpdesk.Interfaces;
using SmartHelpdesk_API; // Namespace của MLModel được tạo bởi ML.NET

namespace SmartHelpdesk.Services;

/// <summary>
/// Service phân tích cảm xúc sử dụng ML.NET model
/// </summary>
public class SentimentService : ISentimentService
{
    private readonly ILogger<SentimentService> _logger;

    public SentimentService(ILogger<SentimentService> logger)
    {
        _logger = logger;
    }

    public SentimentResponse AnalyzeSentiment(string text)
    {
        return AnalyzeSentiment(new SentimentRequest { Text = text });
    }

    public SentimentResponse AnalyzeSentiment(SentimentRequest request)
    {
        try
        {
            // Tạo input cho model
            var input = new MLModel.ModelInput
            {
                Text = request.Text
            };

            // Gọi model để predict
            var prediction = MLModel.Predict(input);

            // Lấy tất cả scores với labels
            var allLabelsScores = MLModel.PredictAllLabels(input);
            var scoresDict = allLabelsScores.ToDictionary(x => x.Key, x => x.Value);

            // Tìm score cao nhất (confidence)
            var maxScore = scoresDict.Count > 0 ? scoresDict.Values.Max() : 0f;

            _logger.LogInformation(
                "Sentiment analyzed: Text='{Text}', Result={Sentiment}, Score={Score:F4}",
                request.Text.Length > 50 ? request.Text[..50] + "..." : request.Text,
                prediction.PredictedLabel,
                maxScore
            );

            return new SentimentResponse
            {
                TicketId = request.TicketId,
                Sentiment = prediction.PredictedLabel ?? "unknown",
                Score = maxScore,
                AllScores = scoresDict
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing sentiment for text: {Text}", request.Text);
            throw;
        }
    }
}
