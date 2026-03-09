# HƯỚNG DẪN TẠO SENTIMENT ANALYSIS API

> **Mục tiêu:** Tạo API endpoint `/api/ai/sentiment` để phân tích cảm xúc từ văn bản sử dụng ML.NET model đã train.

---

## 📁 CÁC FILE ĐÃ ĐƯỢC TẠO TỰ ĐỘNG

ML.NET Model Builder đã tạo các file sau trong thư mục `Backend/`:

| File | Mục đích |
|------|----------|
| `MLModel.mbconfig` | Cấu hình model (scenario, data source, training options) |
| `MLModel.mlnet` | Model đã train (binary file) |
| `MLModel.consumption.cs` | Code để **sử dụng model** (Predict) |
| `MLModel.training.cs` | Code để **train lại model** |

### Cấu Trúc Code Đã Tạo

```csharp
namespace SmartHelpdesk_API
{
    public partial class MLModel
    {
        // Input class
        public class ModelInput
        {
            public string Text { get; set; }      // Văn bản đầu vào
            public string Sentiment { get; set; } // Label (khi train)
        }

        // Output class
        public class ModelOutput
        {
            public string PredictedLabel { get; set; } // Kết quả dự đoán: "positive", "negative", "neutral"
            public float[] Score { get; set; }         // Điểm số cho mỗi label
        }

        // Predict method
        public static ModelOutput Predict(ModelInput input);
    }
}
```

---

## 🛠️ BƯỚC 1: TẠO DTO CHO API

### 1.1. Tạo Request DTO

**File:** `Backend/DTOs/Requests/SentimentRequest.cs`

```csharp
namespace SmartHelpdesk.API.DTOs.Requests;

/// <summary>
/// Request body cho API phân tích cảm xúc
/// </summary>
public class SentimentRequest
{
    /// <summary>
    /// ID của ticket (optional, để tracking)
    /// </summary>
    public Guid? TicketId { get; set; }
    
    /// <summary>
    /// Văn bản cần phân tích cảm xúc
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
```

### 1.2. Tạo Response DTO

**File:** `Backend/DTOs/Responses/SentimentResponse.cs`

```csharp
namespace SmartHelpdesk.API.DTOs.Responses;

/// <summary>
/// Response từ API phân tích cảm xúc
/// </summary>
public class SentimentResponse
{
    /// <summary>
    /// ID của ticket (nếu có trong request)
    /// </summary>
    public Guid? TicketId { get; set; }
    
    /// <summary>
    /// Kết quả cảm xúc: "positive", "negative", "neutral"
    /// </summary>
    public string Sentiment { get; set; } = string.Empty;
    
    /// <summary>
    /// Điểm số confidence (0.0 - 1.0)
    /// </summary>
    public float Score { get; set; }
    
    /// <summary>
    /// Chi tiết điểm số cho từng label
    /// </summary>
    public Dictionary<string, float> AllScores { get; set; } = new();
}
```

---

## 🛠️ BƯỚC 2: TẠO SERVICE

### 2.1. Tạo Interface

**File:** `Backend/Interfaces/ISentimentService.cs`

```csharp
using SmartHelpdesk.API.DTOs.Requests;
using SmartHelpdesk.API.DTOs.Responses;

namespace SmartHelpdesk.API.Interfaces;

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
```

### 2.2. Implement Service

**File:** `Backend/Services/SentimentService.cs`

```csharp
using SmartHelpdesk.API.DTOs.Requests;
using SmartHelpdesk.API.DTOs.Responses;
using SmartHelpdesk.API.Interfaces;
using SmartHelpdesk_API; // Namespace của MLModel

namespace SmartHelpdesk.API.Services;

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
            var maxScore = scoresDict.Values.Max();

            _logger.LogInformation(
                "Sentiment analyzed: Text='{Text}', Result={Sentiment}, Score={Score:F4}",
                request.Text.Length > 50 ? request.Text[..50] + "..." : request.Text,
                prediction.PredictedLabel,
                maxScore
            );

            return new SentimentResponse
            {
                TicketId = request.TicketId,
                Sentiment = prediction.PredictedLabel,
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
```

---

## 🛠️ BƯỚC 3: TẠO CONTROLLER

**File:** `Backend/Controllers/AiController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHelpdesk.API.DTOs.Requests;
using SmartHelpdesk.API.DTOs.Responses;
using SmartHelpdesk.API.Interfaces;

namespace SmartHelpdesk.API.Controllers;

/// <summary>
/// API Controller cho các tính năng AI
/// </summary>
[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly ISentimentService _sentimentService;
    private readonly ILogger<AiController> _logger;

    public AiController(
        ISentimentService sentimentService,
        ILogger<AiController> logger)
    {
        _sentimentService = sentimentService;
        _logger = logger;
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
    public ActionResult<SentimentResponse> AnalyzeSentimentQuick([FromQuery] string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest(new { error = "Text query parameter is required" });
        }

        var result = _sentimentService.AnalyzeSentiment(text);
        return Ok(result);
    }
}
```

---

## 🛠️ BƯỚC 4: ĐĂNG KÝ SERVICE TRONG PROGRAM.CS

Mở file `Backend/Program.cs` và thêm dòng sau vào phần đăng ký services:

```csharp
// ============================================
// Thêm sau các services khác (trước builder.Build())
// ============================================

// AI Services
builder.Services.AddSingleton<ISentimentService, SentimentService>();
```

**Vị trí thêm code:**

```csharp
// ... existing code ...

builder.Services.AddScoped<ITicketsService, TicketsService>();
builder.Services.AddScoped<ICommentsService, CommentsService>();

// 👇 THÊM DÒNG NÀY
builder.Services.AddSingleton<ISentimentService, SentimentService>();

var app = builder.Build();

// ... existing code ...
```

---

## 🛠️ BƯỚC 5: COPY MODEL FILE

Model file `MLModel.mlnet` cần được copy vào output directory khi build.

### 5.1. Cập nhật .csproj

Mở file `Backend/SmartHelpdesk.API.csproj` và thêm:

```xml
<ItemGroup>
  <None Update="MLModel.mlnet">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

---

## ✅ BƯỚC 6: TEST API

### 6.1. Build và Run

```powershell
cd Backend
dotnet build
dotnet run
```

### 6.2. Test với Swagger

Mở browser: `https://localhost:7209/swagger`

Tìm endpoint `POST /api/ai/sentiment` và test với body:

```json
{
  "ticketId": null,
  "text": "Ứng dụng crash liên tục, không thể sử dụng được!"
}
```

### 6.3. Test với PowerShell

```powershell
# Test API
$body = @{
    text = "Ứng dụng crash liên tục, không thể sử dụng được!"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:7209/api/ai/sentiment" `
    -Method POST `
    -Body $body `
    -ContentType "application/json"
```

### 6.4. Kết quả mong đợi

```json
{
  "ticketId": null,
  "sentiment": "negative",
  "score": 0.92,
  "allScores": {
    "negative": 0.92,
    "neutral": 0.05,
    "positive": 0.03
  }
}
```

---

## 🔗 BƯỚC 7: TÍCH HỢP VÀO TICKET CREATION

Sau khi API hoạt động, tích hợp vào `TicketsService.cs`:

```csharp
public class TicketsService : ITicketsService
{
    private readonly ISentimentService _sentimentService;
    
    // Trong constructor, inject ISentimentService
    
    public async Task<TicketResponse> CreateTicketAsync(CreateTicketRequest request, string userId)
    {
        // ... existing validation code ...
        
        // 🔥 Phân tích cảm xúc
        var sentimentResult = _sentimentService.AnalyzeSentiment(request.Description);
        
        // Tự động set Priority dựa trên sentiment
        var priority = sentimentResult.Sentiment == "negative" && sentimentResult.Score > 0.7
            ? Priority.High
            : Priority.Normal;
        
        var ticket = new Ticket
        {
            // ... other properties ...
            Priority = priority,
            // Lưu sentiment score vào DB (nếu có column)
        };
        
        // ... save to database ...
    }
}
```

---

## 📋 CHECKLIST

- [ ] Tạo file `DTOs/Requests/SentimentRequest.cs`
- [ ] Tạo file `DTOs/Responses/SentimentResponse.cs`
- [ ] Tạo file `Interfaces/ISentimentService.cs`
- [ ] Tạo file `Services/SentimentService.cs`
- [ ] Tạo file `Controllers/AiController.cs`
- [ ] Cập nhật `Program.cs` để đăng ký service
- [ ] Cập nhật `SmartHelpdesk.API.csproj` để copy model file
- [ ] Build và test API
- [ ] Tích hợp vào TicketsService

---

## 🐛 TROUBLESHOOTING

### Lỗi "Model file not found"

```
System.IO.FileNotFoundException: Could not find file 'MLModel.mlnet'
```

**Giải pháp:**
1. Kiểm tra file `MLModel.mlnet` có trong folder `Backend/`
2. Cập nhật `.csproj` để copy file khi build
3. Rebuild project: `dotnet build`

### Lỗi namespace không tìm thấy

```
CS0246: The type or namespace name 'SmartHelpdesk_API' could not be found
```

**Giải pháp:**
Thêm `using SmartHelpdesk_API;` vào đầu file `SentimentService.cs`

### Model accuracy thấp

Nếu kết quả không chính xác:
1. Thêm nhiều dữ liệu training vào file CSV
2. Mở `MLModel.mbconfig` trong Visual Studio
3. Click "Improve model" và train lại với thời gian dài hơn

---

> **Tiếp theo:** Sau khi hoàn thành Sentiment API, tiến hành Phase 2: Real-time Chat với SignalR.
