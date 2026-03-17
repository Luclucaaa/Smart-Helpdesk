using System.Text.RegularExpressions;
using SmartHelpdesk.DTOs.Requests;
using SmartHelpdesk.DTOs.Responses;
using SmartHelpdesk.Interfaces;
using SmartHelpdesk_API; // Namespace của MLModel được tạo bởi ML.NET

namespace SmartHelpdesk.Services;

/// <summary>
/// Service phân tích cảm xúc sử dụng ML.NET model kết hợp keyword-based correction
/// cho tiếng Việt (hybrid approach)
/// </summary>
public class SentimentService : ISentimentService
{
    private readonly ILogger<SentimentService> _logger;

    // === VIETNAMESE KEYWORD DICTIONARIES ===
    // Từ khóa tiêu cực (negative) với trọng số
    private static readonly Dictionary<string, float> NegativeKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Từ mạnh (trọng số cao)
        { "tệ", 1.5f }, { "tồi", 1.5f }, { "kinh khủng", 1.5f }, { "thất vọng", 1.5f },
        { "ghét", 1.5f }, { "tức giận", 1.5f }, { "phẫn nộ", 1.5f }, { "khủng khiếp", 1.5f },
        { "rác", 1.2f }, { "vô dụng", 1.2f }, { "thảm họa", 1.5f }, { "tệ hại", 1.5f },
        { "dở", 1.2f }, { "dở tệ", 1.5f }, { "chán", 1.0f },

        // Từ vừa
        { "lỗi", 1.0f }, { "bug", 1.0f }, { "hỏng", 1.0f }, { "trục trặc", 1.0f },
        { "chậm", 0.8f }, { "lag", 0.8f }, { "giật", 0.8f }, { "đứng", 0.7f },
        { "kém", 1.0f }, { "yếu", 0.7f }, { "thiếu", 0.6f },
        { "khó chịu", 1.0f }, { "bực", 1.0f }, { "bực mình", 1.2f },
        { "không hài lòng", 1.5f }, { "không ổn", 1.0f },
        { "không tốt", 1.2f }, { "không được", 1.0f },
        { "không hoạt động", 1.2f }, { "không chạy", 1.0f },
        { "không phản hồi", 1.0f }, { "không thể", 0.8f },
        { "mất", 0.7f }, { "mất dữ liệu", 1.5f },
        { "sập", 1.2f }, { "crash", 1.2f }, { "die", 1.0f },

        // Cụm từ phổ biến
        { "quá tệ", 1.8f }, { "quá chậm", 1.2f }, { "quá kém", 1.5f },
        { "rất tệ", 1.8f }, { "rất kém", 1.5f }, { "rất chậm", 1.2f },
        { "cực kỳ tệ", 2.0f }, { "cực kỳ chậm", 1.5f },
        { "tệ quá", 1.8f }, { "kém quá", 1.5f }, { "chậm quá", 1.2f },
        { "liên tục", 0.5f }, // tăng trọng số khi đi kèm từ negative
    };

    // Từ khóa tích cực (positive) với trọng số
    private static readonly Dictionary<string, float> PositiveKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Từ mạnh
        { "tuyệt vời", 1.5f }, { "xuất sắc", 1.5f }, { "hoàn hảo", 1.5f },
        { "tuyệt hảo", 1.5f }, { "siêu", 1.0f }, { "đỉnh", 1.2f },
        { "yêu thích", 1.2f }, { "thích", 0.8f },

        // Từ vừa
        { "tốt", 1.0f }, { "hay", 0.8f }, { "đẹp", 0.8f }, { "ổn", 0.5f },
        { "hài lòng", 1.2f }, { "vui", 0.8f }, { "dễ dùng", 1.0f }, { "dễ sử dụng", 1.0f },
        { "nhanh", 0.8f }, { "mượt", 0.8f }, { "ổn định", 1.0f },
        { "chuyên nghiệp", 1.0f }, { "hiệu quả", 1.0f },
        { "hỗ trợ tốt", 1.2f }, { "rất tốt", 1.5f }, { "quá tốt", 1.5f },
        { "cảm ơn", 0.8f }, { "cám ơn", 0.8f }, { "biết ơn", 1.0f },
        { "rất hài lòng", 1.8f }, { "rất thích", 1.2f },
        { "tốt quá", 1.5f }, { "tuyệt quá", 1.5f },
    };

    // Từ phủ định - đảo ngược sentiment
    private static readonly HashSet<string> NegationWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "không", "chẳng", "chả", "đâu có", "nào có", "không hề", "chưa", "chưa bao giờ"
    };

    public SentimentService(ILogger<SentimentService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Tiền xử lý text: trim, xóa HTML/URL, gộp khoảng trắng
    /// </summary>
    private static string PreprocessText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var processed = text.Trim();
        processed = Regex.Replace(processed, @"<[^>]+>", " ");
        processed = Regex.Replace(processed, @"https?://\S+|www\.\S+", " ");
        processed = Regex.Replace(processed, @"\s+", " ");
        return processed.Trim();
    }

    /// <summary>
    /// Phân tích keyword-based sentiment cho text tiếng Việt
    /// Trả về (negativeScore, positiveScore)
    /// </summary>
    private (float negativeScore, float positiveScore) AnalyzeKeywords(string text)
    {
        var lowerText = text.ToLowerInvariant();
        float negScore = 0f;
        float posScore = 0f;

        // Kiểm tra cụm từ dài trước (ưu tiên match dài hơn)
        var sortedNegative = NegativeKeywords.OrderByDescending(k => k.Key.Length);
        var sortedPositive = PositiveKeywords.OrderByDescending(k => k.Key.Length);

        // Đếm negative keywords
        foreach (var kv in sortedNegative)
        {
            if (lowerText.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
            {
                negScore += kv.Value;
            }
        }

        // Đếm positive keywords
        foreach (var kv in sortedPositive)
        {
            if (lowerText.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
            {
                posScore += kv.Value;
            }
        }

        // Kiểm tra negation (phủ định) - đảo chiều sentiment
        foreach (var negation in NegationWords)
        {
            if (lowerText.Contains(negation, StringComparison.OrdinalIgnoreCase))
            {
                // Nếu có từ phủ định + từ tích cực → chuyển thành negative
                // VD: "không tốt", "không hài lòng"
                if (posScore > 0 && negScore == 0)
                {
                    negScore = posScore * 0.8f;
                    posScore *= 0.2f;
                }
                break;
            }
        }

        return (negScore, posScore);
    }

    /// <summary>
    /// Kết hợp ML model prediction với keyword analysis (hybrid)
    /// </summary>
    private (string sentiment, float score, Dictionary<string, float> adjustedScores)
        CombinePrediction(
            string mlSentiment,
            Dictionary<string, float> mlScores,
            float keywordNegScore,
            float keywordPosScore)
    {
        var adjustedScores = new Dictionary<string, float>(mlScores);
        var keywordDiff = keywordNegScore - keywordPosScore;
        var totalKeywordScore = keywordNegScore + keywordPosScore;

        // Nếu không có keyword nào match → tin tưởng ML model
        if (totalKeywordScore < 0.5f)
        {
            var topLabel = adjustedScores.OrderByDescending(x => x.Value).First();
            return (topLabel.Key, topLabel.Value, adjustedScores);
        }

        // Keyword analysis cho thấy negative mạnh
        if (keywordNegScore >= 1.5f && keywordDiff > 0.5f)
        {
            // Boost negative score, reduce positive
            if (adjustedScores.ContainsKey("negative"))
                adjustedScores["negative"] = Math.Min(1f, adjustedScores["negative"] + 0.3f + (keywordNegScore * 0.05f));
            if (adjustedScores.ContainsKey("positive"))
                adjustedScores["positive"] = Math.Max(0f, adjustedScores["positive"] - 0.3f - (keywordNegScore * 0.05f));
        }
        // Keyword analysis cho thấy positive mạnh
        else if (keywordPosScore >= 1.5f && keywordDiff < -0.5f)
        {
            if (adjustedScores.ContainsKey("positive"))
                adjustedScores["positive"] = Math.Min(1f, adjustedScores["positive"] + 0.3f + (keywordPosScore * 0.05f));
            if (adjustedScores.ContainsKey("negative"))
                adjustedScores["negative"] = Math.Max(0f, adjustedScores["negative"] - 0.3f - (keywordPosScore * 0.05f));
        }

        // Normalize scores
        var total = adjustedScores.Values.Sum();
        if (total > 0)
        {
            foreach (var key in adjustedScores.Keys.ToList())
                adjustedScores[key] /= total;
        }

        var finalLabel = adjustedScores.OrderByDescending(x => x.Value).First();
        return (finalLabel.Key, finalLabel.Value, adjustedScores);
    }

    public SentimentResponse AnalyzeSentiment(string text)
    {
        return AnalyzeSentiment(new SentimentRequest { Text = text });
    }

    public SentimentResponse AnalyzeSentiment(SentimentRequest request)
    {
        try
        {
            // 1. Tiền xử lý text
            var preprocessedText = PreprocessText(request.Text);

            // 2. ML Model prediction
            var input = new MLModel.ModelInput { Text = preprocessedText };
            var prediction = MLModel.Predict(input);
            var allLabelsScores = MLModel.PredictAllLabels(input);
            var mlScores = allLabelsScores.ToDictionary(x => x.Key, x => x.Value);

            // 3. Keyword-based analysis
            var (keyNegScore, keyPosScore) = AnalyzeKeywords(preprocessedText);

            // 4. Hybrid: kết hợp ML + keyword
            var (finalSentiment, finalScore, adjustedScores) =
                CombinePrediction(prediction.PredictedLabel ?? "unknown", mlScores, keyNegScore, keyPosScore);

            _logger.LogInformation(
                "Sentiment: Text='{Text}', ML={MlLabel}, Keywords(neg={Neg:F1},pos={Pos:F1}), Final={Final}({Score:F4})",
                preprocessedText.Length > 50 ? preprocessedText[..50] + "..." : preprocessedText,
                prediction.PredictedLabel,
                keyNegScore,
                keyPosScore,
                finalSentiment,
                finalScore
            );

            return new SentimentResponse
            {
                TicketId = request.TicketId,
                Sentiment = finalSentiment,
                Score = finalScore,
                AllScores = adjustedScores
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing sentiment for text: {Text}", request.Text);
            throw;
        }
    }
}
