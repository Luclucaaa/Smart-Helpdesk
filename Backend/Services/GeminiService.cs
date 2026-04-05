using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmartHelpdesk.Services
{
    public class GeminiService
    {
        private const string MissingApiKeyMessage = "Chưa cấu hình Gemini API key hợp lệ trong appsettings.json.";
        private const string OutOfScopeMessage = "Mình chưa tìm thấy thông tin phù hợp. Bạn có thể hỏi về cách dùng website: tạo yêu cầu, theo dõi yêu cầu, cập nhật hồ sơ, đăng nhập, đính kèm tệp, hoặc trạng thái xử lý.";

        private static readonly string[] GreetingKeywords =
        {
            "xin chao", "chao", "hello", "hi", "hey", "alo"
        };

        private static readonly string[] ThanksKeywords =
        {
            "cam on", "cảm ơn", "thanks", "thank you"
        };

        private static readonly string[] FarewellKeywords =
        {
            "tam biet", "tạm biệt", "bye", "goodbye", "hen gap lai"
        };

        private static readonly string[] PasswordKeywords =
        {
            "doi mat khau", "đổi mật khẩu", "quen mat khau", "quên mật khẩu", "mat khau"
        };

        private static readonly string[] CreateTicketKeywords =
        {
            "tao yeu cau", "tạo yêu cầu", "gui yeu cau", "gửi yêu cầu", "create ticket", "mo ticket"
        };

        private static readonly string[] MyTicketsKeywords =
        {
            "yeu cau cua toi", "yêu cầu của tôi", "theo doi trang thai", "theo dõi trạng thái", "my tickets", "tra cuu"
        };

        private static readonly string[] AttachmentKeywords =
        {
            "dinh kem", "đính kèm", "upload", "tep", "tệp", "file", "anh", "ảnh"
        };

        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".md", ".txt", ".cs", ".json"
        };

        private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".git", ".vs", "node_modules"
        };

        private readonly string _apiKey;
        private readonly string _model;
        private readonly HttpClient _httpClient;
        private readonly string _projectRoot;
        private readonly int _topK;
        private readonly int _maxContextChars;
        private readonly bool _useVectorDb;
        private readonly string _qdrantUrl;
        private readonly string _qdrantCollection;
        private readonly string? _qdrantApiKey;
        private readonly string _embeddingModel;
        private bool _vectorDbReady;

        private readonly object _indexLock = new();
        private IReadOnlyList<KnowledgeChunk> _chunks = Array.Empty<KnowledgeChunk>();

        public GeminiService(
            string apiKey,
            string model,
            string projectRoot,
            int topK = 5,
            int maxContextChars = 5500,
            bool useVectorDb = false,
            string? qdrantUrl = null,
            string? qdrantCollection = null,
            string? qdrantApiKey = null,
            string? embeddingModel = null)
        {
            _apiKey = apiKey;
            _model = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model;
            _projectRoot = projectRoot;
            _topK = Math.Clamp(topK, 2, 10);
            _maxContextChars = Math.Clamp(maxContextChars, 1500, 12000);
            _useVectorDb = useVectorDb;
            _qdrantUrl = string.IsNullOrWhiteSpace(qdrantUrl) ? "http://localhost:6333" : qdrantUrl.TrimEnd('/');
            _qdrantCollection = string.IsNullOrWhiteSpace(qdrantCollection) ? "smarthelpdesk_kb" : qdrantCollection;
            _qdrantApiKey = qdrantApiKey;
            _embeddingModel = string.IsNullOrWhiteSpace(embeddingModel) ? "text-embedding-004" : embeddingModel;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(120)
            };

            BuildKnowledgeIndex();

            if (_useVectorDb)
            {
                TryInitializeVectorDb();
            }
        }

        public async Task<string> AskGeminiAsync(string question)
        {
            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "YOUR_GEMINI_API_KEY" || _apiKey == "your_gemini_api_key_here")
            {
                return MissingApiKeyMessage;
            }

            if (TryGetSmallTalkReply(question, out var smallTalkReply))
            {
                return smallTalkReply;
            }

            if (TryGetUsageGuideReply(question, out var usageGuideReply))
            {
                return usageGuideReply;
            }

            var retrieval = await RetrieveRelevantContextAsync(question);
            if (retrieval.Count == 0)
            {
                return OutOfScopeMessage;
            }

            var contextBlock = BuildContextBlock(retrieval);

            var prompt = $"""
Cau hoi nguoi dung:
{question.Trim()}

Ngu canh trich xuat tu du an Smart-Helpdesk:
{contextBlock}
""";

            return await GenerateAnswerWithContextAsync(prompt);
        }

        private void TryInitializeVectorDb()
        {
            try
            {
                InitializeVectorDbAsync().GetAwaiter().GetResult();
                _vectorDbReady = true;
            }
            catch
            {
                _vectorDbReady = false;
            }
        }

        private void BuildKnowledgeIndex()
        {
            var collected = new List<KnowledgeChunk>();
            var files = GetKnowledgeFiles(_projectRoot);

            foreach (var file in files)
            {
                var chunks = ChunkFile(file, _projectRoot);
                collected.AddRange(chunks);
            }

            lock (_indexLock)
            {
                _chunks = collected;
            }
        }

        private static IEnumerable<string> GetKnowledgeFiles(string root)
        {
            var allFiles = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories);

            foreach (var file in allFiles)
            {
                if (!SupportedExtensions.Contains(Path.GetExtension(file)))
                {
                    continue;
                }

                var relative = Path.GetRelativePath(root, file);
                var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (parts.Any(IgnoredDirectories.Contains))
                {
                    continue;
                }

                // Keep RAG focused on user-facing docs and UI flows only.
                if (!IsUserFacingKnowledge(relative))
                {
                    continue;
                }

                yield return file;
            }
        }

        private static bool IsUserFacingKnowledge(string relativePath)
        {
            if (relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return relativePath.StartsWith("Frontend\\Pages", StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith("Frontend\\Shared", StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith("Frontend\\Layout", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<KnowledgeChunk> ChunkFile(string filePath, string projectRoot)
        {
            var text = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<KnowledgeChunk>();
            }

            var relativePath = Path.GetRelativePath(projectRoot, filePath).Replace('\\', '/');
            var lines = text.Split('\n');

            const int chunkSize = 30;
            const int overlap = 6;
            var results = new List<KnowledgeChunk>();

            var start = 0;
            while (start < lines.Length)
            {
                var endExclusive = Math.Min(start + chunkSize, lines.Length);
                var chunkLines = lines.Skip(start).Take(endExclusive - start);
                var chunkText = string.Join("\n", chunkLines).Trim();

                if (!string.IsNullOrWhiteSpace(chunkText))
                {
                    results.Add(new KnowledgeChunk(
                        relativePath,
                        start + 1,
                        endExclusive,
                        chunkText,
                        Tokenize(chunkText)));
                }

                if (endExclusive == lines.Length)
                {
                    break;
                }

                start = Math.Max(endExclusive - overlap, start + 1);
            }

            return results;
        }

        private async Task<List<KnowledgeChunk>> RetrieveRelevantContextAsync(string question)
        {
            if (_useVectorDb && _vectorDbReady)
            {
                var vectorResult = await SearchVectorDbAsync(question);
                if (vectorResult.Count > 0)
                {
                    return vectorResult;
                }
            }

            return RetrieveRelevantContextLexical(question);
        }

        private List<KnowledgeChunk> RetrieveRelevantContextLexical(string question)
        {
            var queryTokens = Tokenize(question);
            if (queryTokens.Count == 0)
            {
                return new List<KnowledgeChunk>();
            }

            List<KnowledgeChunk> chunksSnapshot;
            lock (_indexLock)
            {
                chunksSnapshot = _chunks.ToList();
            }

            var ranked = chunksSnapshot
                .Select(chunk => new
                {
                    Chunk = chunk,
                    Score = ComputeScore(chunk, queryTokens)
                })
                .Where(x => x.Score >= 0.55)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Chunk.Path, StringComparer.OrdinalIgnoreCase)
                .Take(_topK)
                .Select(x => x.Chunk)
                .ToList();

            return ranked;
        }

        private async Task InitializeVectorDbAsync()
        {
            await EnsureQdrantCollectionAsync();

            List<KnowledgeChunk> chunksSnapshot;
            lock (_indexLock)
            {
                chunksSnapshot = _chunks.ToList();
            }

            if (chunksSnapshot.Count == 0)
            {
                return;
            }

            const int batchSize = 24;
            var points = new List<object>(batchSize);
            var pointId = 1L;

            foreach (var chunk in chunksSnapshot)
            {
                var vector = await EmbedTextAsync(chunk.Content);
                if (vector.Count == 0)
                {
                    continue;
                }

                points.Add(new
                {
                    id = pointId,
                    vector,
                    payload = new
                    {
                        path = chunk.Path,
                        startLine = chunk.StartLine,
                        endLine = chunk.EndLine,
                        content = chunk.Content
                    }
                });

                pointId++;

                if (points.Count >= batchSize)
                {
                    await UpsertPointsAsync(points);
                    points.Clear();
                }
            }

            if (points.Count > 0)
            {
                await UpsertPointsAsync(points);
            }
        }

        private async Task EnsureQdrantCollectionAsync()
        {
            var getRequest = CreateQdrantRequest(HttpMethod.Get, $"/collections/{_qdrantCollection}");
            var getResponse = await _httpClient.SendAsync(getRequest);
            if (getResponse.IsSuccessStatusCode)
            {
                return;
            }

            if (getResponse.StatusCode != HttpStatusCode.NotFound)
            {
                var details = await getResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Khong the ket noi Qdrant: {details}");
            }

            var createBody = new
            {
                vectors = new
                {
                    size = 768,
                    distance = "Cosine"
                }
            };

            var createRequest = CreateQdrantRequest(HttpMethod.Put, $"/collections/{_qdrantCollection}", createBody);
            var createResponse = await _httpClient.SendAsync(createRequest);
            if (!createResponse.IsSuccessStatusCode)
            {
                var details = await createResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Khong tao duoc collection Qdrant: {details}");
            }
        }

        private async Task UpsertPointsAsync(List<object> points)
        {
            var body = new { points };
            var request = CreateQdrantRequest(HttpMethod.Put, $"/collections/{_qdrantCollection}/points", body);
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var details = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Khong upsert duoc vector points: {details}");
            }
        }

        private async Task<List<KnowledgeChunk>> SearchVectorDbAsync(string question)
        {
            try
            {
                var queryVector = await EmbedTextAsync(question);
                if (queryVector.Count == 0)
                {
                    return new List<KnowledgeChunk>();
                }

                var body = new
                {
                    vector = queryVector,
                    limit = _topK,
                    with_payload = true
                };

                var request = CreateQdrantRequest(HttpMethod.Post, $"/collections/{_qdrantCollection}/points/search", body);
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return new List<KnowledgeChunk>();
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseContent);

                if (!doc.RootElement.TryGetProperty("result", out var resultNode) || resultNode.ValueKind != JsonValueKind.Array)
                {
                    return new List<KnowledgeChunk>();
                }

                var chunks = new List<KnowledgeChunk>();

                foreach (var point in resultNode.EnumerateArray())
                {
                    if (!point.TryGetProperty("payload", out var payloadNode))
                    {
                        continue;
                    }

                    var path = payloadNode.TryGetProperty("path", out var pathNode)
                        ? pathNode.GetString() ?? string.Empty
                        : string.Empty;

                    var content = payloadNode.TryGetProperty("content", out var contentNode)
                        ? contentNode.GetString() ?? string.Empty
                        : string.Empty;

                    var startLine = payloadNode.TryGetProperty("startLine", out var startLineNode) && startLineNode.TryGetInt32(out var s)
                        ? s
                        : 1;

                    var endLine = payloadNode.TryGetProperty("endLine", out var endLineNode) && endLineNode.TryGetInt32(out var e)
                        ? e
                        : startLine;

                    if (string.IsNullOrWhiteSpace(content))
                    {
                        continue;
                    }

                    chunks.Add(new KnowledgeChunk(path, startLine, endLine, content, Tokenize(content)));
                }

                return chunks;
            }
            catch
            {
                _vectorDbReady = false;
                return new List<KnowledgeChunk>();
            }
        }

        private async Task<List<float>> EmbedTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<float>();
            }

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_embeddingModel}:embedContent?key={_apiKey}";
            var body = new
            {
                content = new
                {
                    parts = new[] { new { text } }
                },
                outputDimensionality = 768
            };

            var response = await _httpClient.PostAsync(
                url,
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                return new List<float>();
            }

            var payload = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("embedding", out var embeddingNode) ||
                !embeddingNode.TryGetProperty("values", out var valuesNode) ||
                valuesNode.ValueKind != JsonValueKind.Array)
            {
                return new List<float>();
            }

            var vector = new List<float>(valuesNode.GetArrayLength());
            foreach (var value in valuesNode.EnumerateArray())
            {
                if (value.TryGetSingle(out var number))
                {
                    vector.Add(number);
                }
                else if (float.TryParse(value.ToString(), out var parsed))
                {
                    vector.Add(parsed);
                }
            }

            return vector;
        }

        private HttpRequestMessage CreateQdrantRequest(HttpMethod method, string path, object? body = null)
        {
            var request = new HttpRequestMessage(method, $"{_qdrantUrl}{path}");
            if (!string.IsNullOrWhiteSpace(_qdrantApiKey))
            {
                request.Headers.TryAddWithoutValidation("api-key", _qdrantApiKey);
            }

            if (body is not null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            return request;
        }

        private static bool TryGetSmallTalkReply(string userText, out string reply)
        {
            var normalized = NormalizeText(userText).Trim();

            if (GreetingKeywords.Any(k => normalized.Contains(k, StringComparison.Ordinal)))
            {
                reply = "Chào bạn. Mình có thể hỗ trợ các thao tác trên Smart-Helpdesk như tạo yêu cầu, theo dõi trạng thái và đính kèm tệp.";
                return true;
            }

            if (ThanksKeywords.Any(k => normalized.Contains(k, StringComparison.Ordinal)))
            {
                reply = "Rất vui được hỗ trợ bạn. Nếu cần, bạn cứ hỏi thêm thao tác cụ thể trên website.";
                return true;
            }

            if (FarewellKeywords.Any(k => normalized.Contains(k, StringComparison.Ordinal)))
            {
                reply = "Chào bạn, chúc bạn một ngày tốt lành. Khi cần hỗ trợ, mình luôn sẵn sàng.";
                return true;
            }

            if (normalized is "ok" or "oke" or "vâng" or "vang" or "uh" or "ừ")
            {
                reply = "Mình đang ở đây. Bạn muốn mình hướng dẫn thao tác nào trên Smart-Helpdesk?";
                return true;
            }

            reply = string.Empty;
            return false;
        }

        private static bool TryGetUsageGuideReply(string userText, out string reply)
        {
            var normalized = NormalizeText(userText);

            if (CreateTicketKeywords.Any(k => normalized.Contains(k, StringComparison.Ordinal)))
            {
                reply = "Cách tạo yêu cầu mới:\n1. Vào menu Gửi yêu cầu.\n2. Nhập mô tả vấn đề ở ô Mô tả vấn đề.\n3. Chọn sản phẩm (nếu có).\n4. Đính kèm ảnh/tệp minh họa (nếu cần).\n5. Nhấn Gửi yêu cầu để hoàn tất.";
                return true;
            }

            if (MyTicketsKeywords.Any(k => normalized.Contains(k, StringComparison.Ordinal)))
            {
                reply = "Cách theo dõi yêu cầu:\n1. Vào menu Yêu cầu của tôi.\n2. Dùng bộ lọc Tất cả/Mới/Đang xử lý/Đã xong.\n3. Bấm vào từng yêu cầu để xem chi tiết.\n4. Kiểm tra mốc trạng thái và thời gian cập nhật gần nhất.";
                return true;
            }

            if (PasswordKeywords.Any(k => normalized.Contains(k, StringComparison.Ordinal)))
            {
                reply = "Đổi mật khẩu nhanh:\n1. Mở trang hồ sơ hoặc tài khoản.\n2. Chọn mục Đổi mật khẩu.\n3. Nhập mật khẩu hiện tại và mật khẩu mới.\n4. Nhấn Lưu cập nhật.\nNếu chưa thấy mục này, hãy liên hệ Admin hoặc Agent để được hỗ trợ.";
                return true;
            }

            if (AttachmentKeywords.Any(k => normalized.Contains(k, StringComparison.Ordinal)))
            {
                reply = "Cách đính kèm tệp khi gửi yêu cầu:\n1. Vào trang Gửi yêu cầu.\n2. Ở vùng Đính kèm ảnh/file, kéo-thả tệp hoặc bấm Chọn file.\n3. Chờ tệp hiển thị trong danh sách đính kèm.\n4. Nhấn Gửi yêu cầu để hoàn tất.\nMẹo: ưu tiên ảnh chụp lỗi rõ ràng để được hỗ trợ nhanh hơn.";
                return true;
            }

            reply = string.Empty;
            return false;
        }

        private static string FormatReadableResponse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var text = raw.Trim().Replace("\r\n", "\n", StringComparison.Ordinal);
            text = Regex.Replace(text, "\\*\\*(.*?)\\*\\*", "$1");
            text = Regex.Replace(text, "(?m)^\\s*[-*]+\\s+", "- ");
            text = Regex.Replace(text, "(?m)^\\s*\\d+\\)\\s+", "1. ");
            text = Regex.Replace(text, "\n{3,}", "\n\n");
            return text;
        }

        private static double ComputeScore(KnowledgeChunk chunk, HashSet<string> queryTokens)
        {
            var overlapCount = chunk.Tokens.Count(token => queryTokens.Contains(token));
            if (overlapCount == 0)
            {
                return 0;
            }

            var coverage = (double)overlapCount / queryTokens.Count;
            var density = (double)overlapCount / Math.Max(1, chunk.Tokens.Count);
            return (coverage * 2.0) + (density * 8.0) + overlapCount * 0.05;
        }

        private string BuildContextBlock(IReadOnlyList<KnowledgeChunk> chunks)
        {
            var sb = new StringBuilder();

            foreach (var chunk in chunks)
            {
                var header = $"[Nguon: {chunk.Path}:{chunk.StartLine}-{chunk.EndLine}]";
                if (sb.Length + header.Length + chunk.Content.Length + 4 > _maxContextChars)
                {
                    break;
                }

                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine(header);
                sb.AppendLine(chunk.Content);
            }

            return sb.ToString();
        }

        private async Task<string> GenerateAnswerWithContextAsync(string prompt)
        {
            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
                const int maxAttempts = 3;

                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    var requestBody = new
                    {
                        systemInstruction = new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    text = "Ban la tro ly huong dan su dung website Smart-Helpdesk cho nguoi dung cuoi. Chi duoc su dung thong tin trong ngu canh duoc cung cap. KHONG giai thich code, class, endpoint, database, backend, frontend architecture. Chi tra loi cach thao tac tren giao dien web (vao trang nao, bam nut nao, nhap gi). Van duoc phep phan hoi cac cau giao tiep don gian (chao hoi, cam on, tam biet) bang 1-2 cau than thien. Tra loi ngan gon nhung du y, uu tien 3-5 buoc ro rang neu la cau hoi huong dan."
                                }
                            }
                        },
                        contents = new[]
                        {
                            new { role = "user", parts = new[] { new { text = prompt } } }
                        },
                        generationConfig = new
                        {
                            temperature = 0.1
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
                            return "AI chưa trả về nội dung phù hợp.";
                        }

                        var candidate = candidates[0];
                        if (!candidate.TryGetProperty("content", out var contentObj) ||
                            !contentObj.TryGetProperty("parts", out var parts) ||
                            parts.GetArrayLength() == 0)
                        {
                            return "AI chưa trả về nội dung phù hợp.";
                        }

                        var sb = new StringBuilder();
                        foreach (var part in parts.EnumerateArray())
                        {
                            if (!part.TryGetProperty("text", out var textNode))
                            {
                                continue;
                            }

                            var textPart = textNode.GetString();
                            if (string.IsNullOrWhiteSpace(textPart))
                            {
                                continue;
                            }

                            if (sb.Length > 0)
                            {
                                sb.AppendLine();
                            }

                            sb.Append(textPart.Trim());
                        }

                        var result = FormatReadableResponse(sb.ToString());
                        return string.IsNullOrWhiteSpace(result)
                            ? "AI chưa trả về nội dung phù hợp."
                            : result.Trim();
                    }

                    if ((int)response.StatusCode == 429)
                    {
                        var retrySeconds = TryExtractRetrySeconds(responseString) ?? 5;
                        if (attempt < maxAttempts)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(retrySeconds, 1, 20)));
                            continue;
                        }

                        return $"AI đang tạm quá tải. Vui lòng thử lại sau khoảng {retrySeconds} giây.";
                    }

                    if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
                    {
                        return "Gemini API key không hợp lệ hoặc không đủ quyền.";
                    }

                    if ((int)response.StatusCode == 404)
                    {
                        return "Model Gemini không tồn tại hoặc không hỗ trợ generateContent.";
                    }

                    return $"Gemini API lỗi {(int)response.StatusCode} ({response.ReasonPhrase}).";
                }

                return "Không nhận được phản hồi hợp lệ từ Gemini.";
            }
            catch (Exception ex)
            {
                return $"Lỗi khi gọi Gemini: {ex.Message}";
            }
        }

        private static HashSet<string> Tokenize(string input)
        {
            var normalized = NormalizeText(input);
            var matches = Regex.Matches(normalized, "[a-z0-9_]{2,}");
            var tokens = new HashSet<string>(StringComparer.Ordinal);

            foreach (Match match in matches)
            {
                tokens.Add(match.Value);
            }

            return tokens;
        }

        private static string NormalizeText(string input)
        {
            var normalized = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);

            foreach (var c in normalized)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
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

        private sealed record KnowledgeChunk(
            string Path,
            int StartLine,
            int EndLine,
            string Content,
            HashSet<string> Tokens);
    }
}