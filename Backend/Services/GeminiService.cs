
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmartHelpdesk.Services
{
    public class GeminiService
    {
        private readonly string _apiKey;
        private readonly string _model;
        private readonly HttpClient _httpClient;

        public GeminiService(string apiKey, string model)
        {
            _apiKey = apiKey;
            _model = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model;
            _httpClient = new HttpClient();
        }

        public async Task<string> AskGeminiAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "YOUR_GEMINI_API_KEY")
            {
                return "Chua cau hinh Gemini API key hop le trong appsettings.json.";
            }

            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
                const int maxAttempts = 3;
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    var requestBody = new
                    {
                        contents = new[]
                        {
                            new { parts = new[] { new { text = prompt } } }
                        }
                    };
                    var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(url, content);

                    var responseString = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        using var doc = JsonDocument.Parse(responseString);
                        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                        {
                            return $"Gemini khong tra ve candidates. Noi dung: {responseString}";
                        }

                        var candidate = candidates[0];
                        if (!candidate.TryGetProperty("content", out var contentObj) ||
                            !contentObj.TryGetProperty("parts", out var parts) ||
                            parts.GetArrayLength() == 0)
                        {
                            return $"Gemini tra ve du lieu khong dung dinh dang. Noi dung: {responseString}";
                        }

                        var part0 = parts[0];
                        if (!part0.TryGetProperty("text", out var textNode))
                        {
                            return $"Gemini khong co truong text. Noi dung: {responseString}";
                        }

                        var result = textNode.GetString();
                        return string.IsNullOrWhiteSpace(result) ? "Gemini khong tra ve noi dung." : result;
                    }

                    if ((int)response.StatusCode == 429)
                    {
                        var retrySeconds = TryExtractRetrySeconds(responseString) ?? 5;
                        if (attempt < maxAttempts)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(retrySeconds, 1, 20)));
                            continue;
                        }

                        return $"AI dang tam qua tai (vuot quota). Da thu lai {maxAttempts} lan nhung chua thanh cong. Vui long thu lai sau khoang {retrySeconds} giay, hoac doi API key/cong billing.";
                    }

                    if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
                    {
                        return "Gemini API key khong hop le hoac khong du quyen. Vui long kiem tra lai cau hinh.";
                    }

                    if ((int)response.StatusCode == 404)
                    {
                        return "Model Gemini khong ton tai hoac khong ho tro generateContent. Vui long kiem tra Gemini:Model trong appsettings.json.";
                    }

                    return $"Gemini API loi {(int)response.StatusCode} ({response.ReasonPhrase}).";
                }

                return "Khong nhan duoc phan hoi hop le tu Gemini.";
            }
            catch (Exception ex)
            {
                return $"Loi khi goi Gemini: {ex.Message}";
            }
        }

        private static int? TryExtractRetrySeconds(string responseString)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseString);
                if (!doc.RootElement.TryGetProperty("error", out var errorObj) ||
                    !errorObj.TryGetProperty("details", out var details) ||
                    details.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                foreach (var detail in details.EnumerateArray())
                {
                    if (!detail.TryGetProperty("@type", out var typeNode))
                    {
                        continue;
                    }

                    var type = typeNode.GetString();
                    if (!string.Equals(type, "type.googleapis.com/google.rpc.RetryInfo", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!detail.TryGetProperty("retryDelay", out var retryDelayNode))
                    {
                        return null;
                    }

                    var retryDelay = retryDelayNode.GetString();
                    if (string.IsNullOrWhiteSpace(retryDelay))
                    {
                        return null;
                    }

                    // retryDelay format is usually like "34s" or "34.102s".
                    var numericPart = retryDelay.Trim().TrimEnd('s', 'S');
                    if (!double.TryParse(numericPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                    {
                        return null;
                    }

                    return (int)Math.Ceiling(seconds);
                }
            }
            catch
            {
                return null;
            }

            return null;
        }
    }
}