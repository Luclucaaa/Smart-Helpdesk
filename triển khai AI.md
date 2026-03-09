# KẾ HOẠCH TRIỂN KHAI TÍNH NĂNG AI - SMART HELPDESK

> **Tài liệu hướng dẫn triển khai 3 module AI chính:**
>
> 1. Chatbox trò chuyện thời gian thực
> 2. Phân tích cảm xúc khách hàng
> 3. Gợi ý nội dung nhập liệu

---

## MỤC LỤC

1. [Tổng Quan Kiến Trúc](#1-tổng-quan-kiến-trúc)
2. [Cấu Trúc Thư Mục](#2-cấu-trúc-thư-mục)
3. [Packages Cần Cài Đặt](#3-packages-cần-cài-đặt)
4. [Phase 1: Sentiment Analysis (ML.NET)](#4-phase-1-sentiment-analysis-mlnet)
5. [Phase 2: Real-time Chat (SignalR)](#5-phase-2-real-time-chat-signalr)
6. [Phase 3: LLM Suggest & Canned Responses](#6-phase-3-llm-suggest--canned-responses)
7. [Phase 4: RAG Chatbot](#7-phase-4-rag-chatbot)
8. [Lịch Trình Triển Khai](#8-lịch-trình-triển-khai)
9. [Checklist Hoàn Thành](#9-checklist-hoàn-thành)

---

## 1. TỔNG QUAN KIẾN TRÚC

### 1.1. Sơ Đồ Luồng Dữ Liệu AI

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   KHÁCH HÀNG    │────>│   AI ENGINE     │────>│   NHÂN VIÊN     │
│  (Blazor WASM)  │     │   (Backend)     │     │  (Dashboard)    │
└─────────────────┘     └─────────────────┘     └─────────────────┘
        │                       │                       │
        │                       ▼                       │
        │              ┌─────────────────┐              │
        │              │  ML.NET Models  │              │
        │              │  - Sentiment    │              │
        │              │  - Classify     │              │
        │              └─────────────────┘              │
        │                       │                       │
        │                       ▼                       │
        │              ┌─────────────────┐              │
        └─────────────>│   SignalR Hub   │<─────────────┘
                       │  (Real-time)    │
                       └─────────────────┘
                                │
                                ▼
                       ┌─────────────────┐
                       │   LLM Service   │
                       │  (OpenAI/Azure) │
                       └─────────────────┘
```

### 1.2. Các Module AI Chính

| Module                    | Công nghệ       | Mục đích                                |
| ------------------------- | --------------- | --------------------------------------- |
| **Sentiment Analysis**    | ML.NET          | Phân tích cảm xúc, tự động set Priority |
| **Ticket Classification** | ML.NET          | Phân loại Bug/Feature/Support/Sale      |
| **Real-time Chat**        | SignalR         | Chat thời gian thực Customer ↔ Agent    |
| **LLM Suggest**           | OpenAI/Azure    | Gợi ý câu trả lời cho Agent             |
| **RAG Chatbot**           | Vector DB + LLM | Tự động trả lời từ Knowledge Base       |

---

## 2. CẤU TRÚC THƯ MỤC

### 2.1. Backend Structure

```
Backend/
├── AI/
│   ├── Models/                    # ML.NET trained models (.zip)
│   │   ├── SentimentModel.zip
│   │   └── ClassificationModel.zip
│   ├── TrainingData/              # CSV training files
│   │   ├── sentiment_data.csv
│   │   └── classification_data.csv
│   └── Prompts/                   # LLM prompt templates
│       ├── suggest_response.txt
│       └── summarize_ticket.txt
│
├── Hubs/
│   └── ChatHub.cs                 # SignalR Hub
│
├── Interfaces/
│   ├── ISentimentService.cs
│   ├── IClassificationService.cs
│   ├── IChatService.cs
│   ├── ILlmService.cs
│   └── IRagService.cs
│
├── Services/
│   ├── SentimentService.cs
│   ├── ClassificationService.cs
│   ├── ChatService.cs
│   ├── LlmService.cs
│   └── RagService.cs
│
├── Controllers/
│   ├── AiController.cs            # /api/ai/*
│   └── ChatController.cs          # /api/chat/*
│
└── Data/
    └── Entities/
        ├── ChatMessage.cs
        └── AiAnalysisLog.cs
```

### 2.2. Frontend Structure

```
Frontend/
├── Components/
│   ├── Chat/
│   │   ├── ChatBox.razor
│   │   ├── ChatMessage.razor
│   │   └── TypingIndicator.razor
│   └── AI/
│       ├── SentimentBadge.razor
│       └── SuggestionPanel.razor
│
├── Services/
│   ├── ChatHubService.cs
│   └── AiApiService.cs
│
└── Pages/
    └── CustomerPortal/
        └── TicketChat.razor
```

---

## 3. PACKAGES CẦN CÀI ĐẶT

### 3.1. Backend Packages

```powershell
# Di chuyển vào thư mục Backend
cd Backend

# ML.NET cho Sentiment Analysis & Classification
dotnet add package Microsoft.ML --version 3.0.1
dotnet add package Microsoft.ML.FastTree --version 3.0.1

# SignalR cho Real-time Chat
dotnet add package Microsoft.AspNetCore.SignalR.Core

# Azure OpenAI SDK (nếu dùng Azure)
dotnet add package Azure.AI.OpenAI --version 1.0.0-beta.17

# Hoặc OpenAI SDK (nếu dùng OpenAI trực tiếp)
dotnet add package OpenAI --version 1.11.0

# Semantic Kernel (optional - cho RAG nâng cao)
dotnet add package Microsoft.SemanticKernel --version 1.6.3
```

### 3.2. Frontend Packages

```powershell
# Di chuyển vào thư mục Frontend
cd Frontend

# SignalR Client
dotnet add package Microsoft.AspNetCore.SignalR.Client --version 8.0.0
```

---

## 4. PHASE 1: SENTIMENT ANALYSIS (ML.NET)

> **Mục tiêu:** Tự động phân tích cảm xúc khách hàng và set Priority cho ticket.

### 4.1. Bước 1: Tạo Training Data

**File:** `Backend/AI/TrainingData/sentiment_data.csv`

```csv
Text,Sentiment
"Ứng dụng chạy rất mượt, cảm ơn đội ngũ!",1
"Tuyệt vời, vấn đề đã được giải quyết nhanh chóng",1
"Rất hài lòng với dịch vụ hỗ trợ",1
"Lỗi liên tục, không thể sử dụng được!",0
"Quá tệ, đã báo nhiều lần mà không ai xử lý",0
"Ứng dụng crash mỗi khi mở, rất khó chịu",0
"Tôi cần hỗ trợ cài đặt phần mềm",1
"Không đăng nhập được vào hệ thống",0
"Phần mềm hoạt động ổn định",1
"Chờ đợi quá lâu, thất vọng quá!",0
```

> **Lưu ý:** Cần ít nhất 100-500 mẫu để model chính xác.

### 4.2. Bước 2: Định Nghĩa Data Models

**File:** `Backend/AI/Models/SentimentData.cs`

```csharp
using Microsoft.ML.Data;

namespace SmartHelpdesk.API.AI.Models;

/// <summary>
/// Input data for sentiment prediction
/// </summary>
public class SentimentInput
{
    [LoadColumn(0)]
    public string Text { get; set; } = string.Empty;

    [LoadColumn(1), ColumnName("Label")]
    public bool Sentiment { get; set; }
}

/// <summary>
/// Output from sentiment prediction
/// </summary>
public class SentimentPrediction
{
    [ColumnName("PredictedLabel")]
    public bool Prediction { get; set; }

    public float Probability { get; set; }

    public float Score { get; set; }
}
```

### 4.3. Bước 3: Tạo Interface

**File:** `Backend/Interfaces/ISentimentService.cs`

```csharp
namespace SmartHelpdesk.API.Interfaces;

public interface ISentimentService
{
    /// <summary>
    /// Train model từ CSV file
    /// </summary>
    Task TrainModelAsync(string dataPath);

    /// <summary>
    /// Load model đã train
    /// </summary>
    void LoadModel(string modelPath);

    /// <summary>
    /// Predict sentiment từ text
    /// </summary>
    SentimentResult Predict(string text);
}

public record SentimentResult(
    string Sentiment,      // "positive" | "negative"
    float Score,           // 0.0 - 1.0
    float Probability      // Confidence level
);
```

### 4.4. Bước 4: Implement Service

**File:** `Backend/Services/SentimentService.cs`

```csharp
using Microsoft.ML;
using SmartHelpdesk.API.AI.Models;
using SmartHelpdesk.API.Interfaces;

namespace SmartHelpdesk.API.Services;

public class SentimentService : ISentimentService
{
    private readonly MLContext _mlContext;
    private ITransformer? _model;
    private PredictionEngine<SentimentInput, SentimentPrediction>? _predictionEngine;
    private readonly ILogger<SentimentService> _logger;

    public SentimentService(ILogger<SentimentService> logger)
    {
        _mlContext = new MLContext(seed: 42);
        _logger = logger;
    }

    public async Task TrainModelAsync(string dataPath)
    {
        _logger.LogInformation("Starting model training from {DataPath}", dataPath);

        // Load data
        var dataView = _mlContext.Data.LoadFromTextFile<SentimentInput>(
            dataPath,
            hasHeader: true,
            separatorChar: ','
        );

        // Split data: 80% train, 20% test
        var splitData = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2);

        // Build pipeline
        var pipeline = _mlContext.Transforms.Text
            .FeaturizeText("Features", nameof(SentimentInput.Text))
            .Append(_mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: "Label",
                featureColumnName: "Features"
            ));

        // Train
        _model = await Task.Run(() => pipeline.Fit(splitData.TrainSet));

        // Evaluate
        var predictions = _model.Transform(splitData.TestSet);
        var metrics = _mlContext.BinaryClassification.Evaluate(predictions);

        _logger.LogInformation(
            "Model trained. Accuracy: {Accuracy:P2}, F1: {F1:P2}",
            metrics.Accuracy,
            metrics.F1Score
        );

        // Save model
        var modelPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "AI", "Models", "SentimentModel.zip"
        );
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        _mlContext.Model.Save(_model, dataView.Schema, modelPath);

        // Create prediction engine
        _predictionEngine = _mlContext.Model.CreatePredictionEngine<SentimentInput, SentimentPrediction>(_model);
    }

    public void LoadModel(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Model not found at {modelPath}");
        }

        _model = _mlContext.Model.Load(modelPath, out _);
        _predictionEngine = _mlContext.Model.CreatePredictionEngine<SentimentInput, SentimentPrediction>(_model);

        _logger.LogInformation("Model loaded from {ModelPath}", modelPath);
    }

    public SentimentResult Predict(string text)
    {
        if (_predictionEngine == null)
        {
            throw new InvalidOperationException("Model not loaded. Call LoadModel() first.");
        }

        var input = new SentimentInput { Text = text };
        var prediction = _predictionEngine.Predict(input);

        return new SentimentResult(
            Sentiment: prediction.Prediction ? "positive" : "negative",
            Score: prediction.Score,
            Probability: prediction.Probability
        );
    }
}
```

### 4.5. Bước 5: Tạo API Controller

**File:** `Backend/Controllers/AiController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHelpdesk.API.Interfaces;

namespace SmartHelpdesk.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
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
    /// Phân tích cảm xúc từ text
    /// </summary>
    [HttpPost("sentiment")]
    public ActionResult<SentimentResponse> AnalyzeSentiment([FromBody] SentimentRequest request)
    {
        try
        {
            var result = _sentimentService.Predict(request.Text);

            return Ok(new SentimentResponse
            {
                TicketId = request.TicketId,
                Sentiment = result.Sentiment,
                Score = result.Score,
                Probability = result.Probability
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing sentiment");
            return StatusCode(500, new { error = "Failed to analyze sentiment" });
        }
    }
}

// DTOs
public record SentimentRequest(string? TicketId, string Text);

public record SentimentResponse
{
    public string? TicketId { get; init; }
    public string Sentiment { get; init; } = string.Empty;
    public float Score { get; init; }
    public float Probability { get; init; }
}
```

### 4.6. Bước 6: Đăng Ký Service

**Thêm vào `Program.cs`:**

```csharp
// Register AI Services
builder.Services.AddSingleton<ISentimentService, SentimentService>();

// ...

// Load ML model on startup
var app = builder.Build();

// Initialize Sentiment Model
using (var scope = app.Services.CreateScope())
{
    var sentimentService = scope.ServiceProvider.GetRequiredService<ISentimentService>();
    var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AI", "Models", "SentimentModel.zip");

    if (File.Exists(modelPath))
    {
        sentimentService.LoadModel(modelPath);
    }
    else
    {
        // Train if model doesn't exist
        var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AI", "TrainingData", "sentiment_data.csv");
        if (File.Exists(dataPath))
        {
            await sentimentService.TrainModelAsync(dataPath);
        }
    }
}
```

### 4.7. Bước 7: Tích Hợp Vào Ticket Creation

**Cập nhật `TicketsService.cs`:**

```csharp
public async Task<TicketResponse> CreateTicketAsync(CreateTicketRequest request, string userId)
{
    // ... existing code ...

    // AI: Analyze sentiment
    var sentimentResult = _sentimentService.Predict(request.Description);

    // Auto-set priority based on sentiment
    var priority = sentimentResult.Sentiment == "negative" && sentimentResult.Score < 0.4
        ? Priority.High
        : Priority.Normal;

    var ticket = new Ticket
    {
        // ... other properties ...
        Priority = priority,
        SentimentScore = sentimentResult.Score,
        SentimentLabel = sentimentResult.Sentiment
    };

    // ... save ticket ...
}
```

---

## 5. PHASE 2: REAL-TIME CHAT (SIGNALR)

> **Mục tiêu:** Xây dựng chat thời gian thực giữa Customer và Agent.

### 5.1. Bước 1: Tạo ChatMessage Entity

**File:** `Backend/Data/Entities/ChatMessage.cs`

```csharp
namespace SmartHelpdesk.API.Data.Entities;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty; // "Customer" | "Agent"
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;

    // Navigation
    public virtual Ticket Ticket { get; set; } = null!;
}
```

### 5.2. Bước 2: Tạo SignalR Hub

**File:** `Backend/Hubs/ChatHub.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SmartHelpdesk.API.Interfaces;

namespace SmartHelpdesk.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IChatService chatService, ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Join a ticket's chat room
    /// </summary>
    public async Task JoinTicket(string ticketId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, ticketId);
        _logger.LogInformation("User {UserId} joined ticket {TicketId}",
            Context.UserIdentifier, ticketId);
    }

    /// <summary>
    /// Leave a ticket's chat room
    /// </summary>
    public async Task LeaveTicket(string ticketId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ticketId);
    }

    /// <summary>
    /// Send a message to a ticket's chat room
    /// </summary>
    public async Task SendMessage(string ticketId, string message)
    {
        var userId = Context.UserIdentifier!;

        // Save message to database
        var chatMessage = await _chatService.SaveMessageAsync(
            Guid.Parse(ticketId),
            userId,
            message
        );

        // Broadcast to all users in the ticket room
        await Clients.Group(ticketId).SendAsync("ReceiveMessage", new
        {
            chatMessage.Id,
            chatMessage.TicketId,
            chatMessage.SenderId,
            chatMessage.SenderName,
            chatMessage.SenderRole,
            chatMessage.Content,
            chatMessage.CreatedAt
        });
    }

    /// <summary>
    /// Notify others that user is typing
    /// </summary>
    public async Task Typing(string ticketId, bool isTyping)
    {
        await Clients.OthersInGroup(ticketId).SendAsync("UserTyping", new
        {
            UserId = Context.UserIdentifier,
            TicketId = ticketId,
            IsTyping = isTyping
        });
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("User {UserId} connected", Context.UserIdentifier);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("User {UserId} disconnected", Context.UserIdentifier);
        await base.OnDisconnectedAsync(exception);
    }
}
```

### 5.3. Bước 3: Tạo ChatService

**File:** `Backend/Interfaces/IChatService.cs`

```csharp
using SmartHelpdesk.API.Data.Entities;

namespace SmartHelpdesk.API.Interfaces;

public interface IChatService
{
    Task<ChatMessage> SaveMessageAsync(Guid ticketId, string userId, string content);
    Task<IEnumerable<ChatMessage>> GetTicketMessagesAsync(Guid ticketId, int skip = 0, int take = 50);
    Task MarkAsReadAsync(Guid ticketId, string userId);
}
```

**File:** `Backend/Services/ChatService.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using SmartHelpdesk.API.Data;
using SmartHelpdesk.API.Data.Entities;
using SmartHelpdesk.API.Interfaces;

namespace SmartHelpdesk.API.Services;

public class ChatService : IChatService
{
    private readonly SmartHelpdeskContext _context;
    private readonly ILogger<ChatService> _logger;

    public ChatService(SmartHelpdeskContext context, ILogger<ChatService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ChatMessage> SaveMessageAsync(Guid ticketId, string userId, string content)
    {
        var user = await _context.Users.FindAsync(userId);
        var userRoles = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .ToListAsync();

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            SenderId = userId,
            SenderName = user?.UserName ?? "Unknown",
            SenderRole = userRoles.Contains("Admin") || userRoles.Contains("Agent") ? "Agent" : "Customer",
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Message saved for ticket {TicketId}", ticketId);

        return message;
    }

    public async Task<IEnumerable<ChatMessage>> GetTicketMessagesAsync(Guid ticketId, int skip = 0, int take = 50)
    {
        return await _context.ChatMessages
            .Where(m => m.TicketId == ticketId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(Guid ticketId, string userId)
    {
        await _context.ChatMessages
            .Where(m => m.TicketId == ticketId && m.SenderId != userId && !m.IsRead)
            .ExecuteUpdateAsync(m => m.SetProperty(x => x.IsRead, true));
    }
}
```

### 5.4. Bước 4: Cấu Hình SignalR

**Cập nhật `Program.cs`:**

```csharp
// Add SignalR
builder.Services.AddSignalR();
builder.Services.AddScoped<IChatService, ChatService>();

// ...

var app = builder.Build();

// ...

// Map SignalR Hub
app.MapHub<ChatHub>("/hubs/chat");
```

### 5.5. Bước 5: Frontend Chat Component

**File:** `Frontend/Components/Chat/ChatBox.razor`

```razor
@using Microsoft.AspNetCore.SignalR.Client
@inject NavigationManager Navigation
@implements IAsyncDisposable

<div class="chat-container">
    <div class="chat-messages" @ref="messagesContainer">
        @foreach (var message in messages)
        {
            <div class="message @(message.SenderRole == "Agent" ? "agent" : "customer")">
                <div class="message-header">
                    <span class="sender-name">@message.SenderName</span>
                    <span class="timestamp">@message.CreatedAt.ToString("HH:mm")</span>
                </div>
                <div class="message-content">@message.Content</div>
            </div>
        }

        @if (isOtherTyping)
        {
            <div class="typing-indicator">
                <span>Đang nhập...</span>
            </div>
        }
    </div>

    <div class="chat-input">
        <input @bind="currentMessage"
               @bind:event="oninput"
               @onkeypress="HandleKeyPress"
               @onfocus="() => SendTyping(true)"
               @onblur="() => SendTyping(false)"
               placeholder="Nhập tin nhắn..." />
        <button @onclick="SendMessage" disabled="@(string.IsNullOrWhiteSpace(currentMessage))">
            Gửi
        </button>
    </div>
</div>

@code {
    [Parameter] public string TicketId { get; set; } = string.Empty;
    [Parameter] public string AccessToken { get; set; } = string.Empty;

    private HubConnection? hubConnection;
    private List<ChatMessageDto> messages = new();
    private string currentMessage = string.Empty;
    private bool isOtherTyping = false;
    private ElementReference messagesContainer;

    protected override async Task OnInitializedAsync()
    {
        hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/hubs/chat"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(AccessToken)!;
            })
            .WithAutomaticReconnect()
            .Build();

        // Listen for messages
        hubConnection.On<ChatMessageDto>("ReceiveMessage", (message) =>
        {
            messages.Add(message);
            InvokeAsync(StateHasChanged);
        });

        // Listen for typing indicator
        hubConnection.On<TypingDto>("UserTyping", (typing) =>
        {
            isOtherTyping = typing.IsTyping;
            InvokeAsync(StateHasChanged);
        });

        await hubConnection.StartAsync();
        await hubConnection.SendAsync("JoinTicket", TicketId);
    }

    private async Task SendMessage()
    {
        if (!string.IsNullOrWhiteSpace(currentMessage) && hubConnection is not null)
        {
            await hubConnection.SendAsync("SendMessage", TicketId, currentMessage);
            currentMessage = string.Empty;
        }
    }

    private async Task SendTyping(bool isTyping)
    {
        if (hubConnection is not null)
        {
            await hubConnection.SendAsync("Typing", TicketId, isTyping);
        }
    }

    private async Task HandleKeyPress(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SendMessage();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            await hubConnection.SendAsync("LeaveTicket", TicketId);
            await hubConnection.DisposeAsync();
        }
    }

    // DTOs
    public record ChatMessageDto(
        Guid Id,
        Guid TicketId,
        string SenderId,
        string SenderName,
        string SenderRole,
        string Content,
        DateTime CreatedAt
    );

    public record TypingDto(string UserId, string TicketId, bool IsTyping);
}
```

---

## 6. PHASE 3: LLM SUGGEST & CANNED RESPONSES

> **Mục tiêu:** Gợi ý câu trả lời cho Agent sử dụng LLM.

### 6.1. Bước 1: Cấu Hình OpenAI

**Thêm vào `appsettings.json`:**

```json
{
  "OpenAI": {
    "ApiKey": "sk-your-api-key",
    "Model": "gpt-4o-mini",
    "MaxTokens": 500,
    "Temperature": 0.7
  },
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-azure-key",
    "DeploymentName": "gpt-4o"
  }
}
```

### 6.2. Bước 2: Tạo LLM Service

**File:** `Backend/Interfaces/ILlmService.cs`

```csharp
namespace SmartHelpdesk.API.Interfaces;

public interface ILlmService
{
    Task<List<SuggestionResponse>> GenerateSuggestionsAsync(SuggestionRequest request);
    Task<string> SummarizeTicketAsync(Guid ticketId);
}

public record SuggestionRequest(
    Guid TicketId,
    string Context,
    int MaxSuggestions = 3
);

public record SuggestionResponse(
    string Id,
    string Text,
    string Model,
    float Confidence
);
```

**File:** `Backend/Services/LlmService.cs`

```csharp
using OpenAI;
using OpenAI.Chat;
using SmartHelpdesk.API.Interfaces;

namespace SmartHelpdesk.API.Services;

public class LlmService : ILlmService
{
    private readonly ChatClient _chatClient;
    private readonly IConfiguration _config;
    private readonly ILogger<LlmService> _logger;

    public LlmService(IConfiguration config, ILogger<LlmService> logger)
    {
        _config = config;
        _logger = logger;

        var apiKey = _config["OpenAI:ApiKey"];
        var model = _config["OpenAI:Model"] ?? "gpt-4o-mini";

        _chatClient = new ChatClient(model: model, apiKey: apiKey);
    }

    public async Task<List<SuggestionResponse>> GenerateSuggestionsAsync(SuggestionRequest request)
    {
        var systemPrompt = """
            Bạn là trợ lý hỗ trợ khách hàng chuyên nghiệp.
            Dựa vào nội dung ticket và lịch sử chat, hãy đề xuất 3 câu trả lời phù hợp.
            Mỗi câu trả lời nên:
            - Lịch sự, chuyên nghiệp
            - Giải quyết vấn đề của khách hàng
            - Ngắn gọn nhưng đầy đủ thông tin

            Trả về dạng JSON array với format:
            [{"text": "Nội dung gợi ý 1"}, {"text": "Nội dung gợi ý 2"}, {"text": "Nội dung gợi ý 3"}]
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage($"Ticket context:\n{request.Context}")
        };

        try
        {
            var completion = await _chatClient.CompleteChatAsync(messages);
            var responseText = completion.Value.Content[0].Text;

            // Parse JSON response
            var suggestions = System.Text.Json.JsonSerializer.Deserialize<List<SuggestionItem>>(responseText);

            return suggestions?.Select((s, i) => new SuggestionResponse(
                Id: $"s{i + 1}",
                Text: s.Text,
                Model: _config["OpenAI:Model"] ?? "gpt-4o-mini",
                Confidence: 0.9f
            )).ToList() ?? new List<SuggestionResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating suggestions");
            return new List<SuggestionResponse>();
        }
    }

    public async Task<string> SummarizeTicketAsync(Guid ticketId)
    {
        // Implementation for ticket summarization
        // ...
        await Task.CompletedTask;
        return "Summary placeholder";
    }

    private record SuggestionItem(string Text);
}
```

### 6.3. Bước 3: API Endpoint cho Suggestions

**Thêm vào `AiController.cs`:**

```csharp
/// <summary>
/// Sinh gợi ý câu trả lời cho Agent
/// </summary>
[HttpPost("llm/suggest")]
public async Task<ActionResult<LlmSuggestResponse>> GenerateSuggestions(
    [FromBody] LlmSuggestRequest request)
{
    try
    {
        var suggestions = await _llmService.GenerateSuggestionsAsync(new SuggestionRequest(
            TicketId: Guid.Parse(request.TicketId),
            Context: request.PromptContext,
            MaxSuggestions: request.MaxSuggestions
        ));

        return Ok(new LlmSuggestResponse
        {
            TicketId = request.TicketId,
            Suggestions = suggestions
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error generating suggestions");
        return StatusCode(500, new { error = "Failed to generate suggestions" });
    }
}

// DTOs
public record LlmSuggestRequest(
    string TicketId,
    string PromptContext,
    int MaxSuggestions = 3
);

public record LlmSuggestResponse
{
    public string TicketId { get; init; } = string.Empty;
    public List<SuggestionResponse> Suggestions { get; init; } = new();
}
```

### 6.4. Bước 4: Canned Responses

**File:** `Backend/Data/Entities/CannedResponse.cs`

```csharp
namespace SmartHelpdesk.API.Data.Entities;

public class CannedResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Bug, Feature, Support, Sale
    public Guid? ProductId { get; set; }
    public string Language { get; set; } = "vi";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

**API Endpoint:**

```csharp
/// <summary>
/// Lấy danh sách câu trả lời mẫu
/// </summary>
[HttpGet("canned")]
public async Task<ActionResult<CannedResponsesDto>> GetCannedResponses(
    [FromQuery] Guid? productId,
    [FromQuery] string? category)
{
    var query = _context.CannedResponses.Where(c => c.IsActive);

    if (productId.HasValue)
        query = query.Where(c => c.ProductId == null || c.ProductId == productId);

    if (!string.IsNullOrEmpty(category))
        query = query.Where(c => c.Category == category);

    var templates = await query.Select(c => new CannedTemplate
    {
        Id = c.Id.ToString(),
        Title = c.Title,
        Body = c.Body,
        Language = c.Language
    }).ToListAsync();

    return Ok(new CannedResponsesDto { Templates = templates });
}
```

---

## 7. PHASE 4: RAG CHATBOT

> **Mục tiêu:** Xây dựng chatbot tự động trả lời từ Knowledge Base.

### 7.1. Kiến Trúc RAG

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│   Customer   │────>│  RAG Query   │────>│   Vector DB  │
│    Query     │     │   Service    │     │ (Embeddings) │
└──────────────┘     └──────────────┘     └──────────────┘
                            │                     │
                            ▼                     │
                     ┌──────────────┐             │
                     │   LLM (GPT)  │<────────────┘
                     │  + Context   │  (Top K documents)
                     └──────────────┘
                            │
                            ▼
                     ┌──────────────┐
                     │   Response   │
                     │  + Sources   │
                     └──────────────┘
```

### 7.2. Bước 1: Setup Vector Database

**Option A: In-Memory (cho Development)**

```csharp
// Sử dụng Microsoft.SemanticKernel.Memory
builder.Services.AddSingleton<IMemoryStore, VolatileMemoryStore>();
```

**Option B: Qdrant (cho Production)**

```powershell
# Run Qdrant via Docker
docker run -p 6333:6333 qdrant/qdrant
```

### 7.3. Bước 2: Tạo RAG Service

**File:** `Backend/Interfaces/IRagService.cs`

```csharp
namespace SmartHelpdesk.API.Interfaces;

public interface IRagService
{
    Task<RagResponse> QueryAsync(RagQueryRequest request);
    Task IndexDocumentAsync(RagDocument document);
    Task ReindexAllAsync();
}

public record RagQueryRequest(
    string? SessionId,
    string Query,
    Guid? ProductId,
    int TopK = 5
);

public record RagResponse(
    string Answer,
    List<RagSource> Sources,
    string Model,
    long RetrievalTimeMs
);

public record RagSource(
    string DocId,
    string Title,
    string Snippet,
    float Score,
    string? Url
);

public record RagDocument(
    string DocId,
    string Content,
    string Title,
    Guid? ProductId,
    string? Url
);
```

**File:** `Backend/Services/RagService.cs`

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using SmartHelpdesk.API.Interfaces;

namespace SmartHelpdesk.API.Services;

public class RagService : IRagService
{
    private readonly ISemanticTextMemory _memory;
    private readonly Kernel _kernel;
    private readonly ILogger<RagService> _logger;
    private const string CollectionName = "helpdesk_kb";

    public RagService(
        ISemanticTextMemory memory,
        Kernel kernel,
        ILogger<RagService> logger)
    {
        _memory = memory;
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<RagResponse> QueryAsync(RagQueryRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 1. Search vector DB for relevant documents
        var searchResults = await _memory.SearchAsync(
            collection: CollectionName,
            query: request.Query,
            limit: request.TopK,
            minRelevanceScore: 0.5
        ).ToListAsync();

        // 2. Build context from retrieved documents
        var context = string.Join("\n\n", searchResults.Select(r =>
            $"[{r.Metadata.Id}] {r.Metadata.Description}\n{r.Metadata.Text}"
        ));

        // 3. Generate answer using LLM
        var prompt = $"""
            Dựa trên các tài liệu sau đây, hãy trả lời câu hỏi của khách hàng.
            Nếu không tìm thấy thông tin phù hợp, hãy nói "Tôi không có thông tin về vấn đề này."

            Tài liệu:
            {context}

            Câu hỏi: {request.Query}

            Trả lời:
            """;

        var result = await _kernel.InvokePromptAsync(prompt);
        var answer = result.GetValue<string>() ?? "Không thể tạo câu trả lời.";

        stopwatch.Stop();

        return new RagResponse(
            Answer: answer,
            Sources: searchResults.Select(r => new RagSource(
                DocId: r.Metadata.Id,
                Title: r.Metadata.Description,
                Snippet: r.Metadata.Text.Length > 200
                    ? r.Metadata.Text[..200] + "..."
                    : r.Metadata.Text,
                Score: (float)(r.Relevance ?? 0),
                Url: r.Metadata.AdditionalMetadata
            )).ToList(),
            Model: "gpt-4o-mini",
            RetrievalTimeMs: stopwatch.ElapsedMilliseconds
        );
    }

    public async Task IndexDocumentAsync(RagDocument document)
    {
        await _memory.SaveInformationAsync(
            collection: CollectionName,
            id: document.DocId,
            text: document.Content,
            description: document.Title,
            additionalMetadata: document.Url
        );

        _logger.LogInformation("Indexed document {DocId}", document.DocId);
    }

    public async Task ReindexAllAsync()
    {
        // Implementation for reindexing all KB documents
        await Task.CompletedTask;
    }
}
```

### 7.4. Bước 3: API Endpoint

**Thêm vào `ChatController.cs`:**

```csharp
[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly IRagService _ragService;

    public ChatController(IRagService ragService)
    {
        _ragService = ragService;
    }

    /// <summary>
    /// RAG Query - Chatbot trả lời từ Knowledge Base
    /// </summary>
    [HttpPost("rag/query")]
    public async Task<ActionResult<RagResponse>> RagQuery([FromBody] RagQueryRequest request)
    {
        var response = await _ragService.QueryAsync(request);
        return Ok(response);
    }

    /// <summary>
    /// Index document vào Vector DB
    /// </summary>
    [HttpPost("rag/reindex")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReindexDocument([FromBody] RagDocument document)
    {
        await _ragService.IndexDocumentAsync(document);
        return Ok(new { status = "ok" });
    }
}
```

---

## 8. LỊCH TRÌNH TRIỂN KHAI

| Phase | Tuần | Nội dung                    | Output                          |
| ----- | ---- | --------------------------- | ------------------------------- |
| **1** | 1-2  | Sentiment Analysis (ML.NET) | Model + API `/api/ai/sentiment` |
| **1** | 2-3  | Ticket Classification       | Model + API `/api/ai/classify`  |
| **2** | 3-4  | SignalR Chat Hub            | Real-time chat hoạt động        |
| **2** | 4-5  | Frontend Chat Component     | Blazor ChatBox.razor            |
| **3** | 5-6  | LLM Suggest Service         | API `/api/ai/llm/suggest`       |
| **3** | 6-7  | Canned Responses            | API `/api/ai/canned`            |
| **4** | 7-8  | RAG Chatbot                 | API `/api/chat/rag/query`       |
| **4** | 8-9  | Testing & Optimization      | Full AI suite hoạt động         |

---

## 9. CHECKLIST HOÀN THÀNH

### Phase 1: Sentiment Analysis

- [ ] Cài đặt Microsoft.ML packages
- [ ] Tạo training data CSV
- [ ] Implement SentimentService
- [ ] Create API endpoint
- [ ] Integrate với Ticket creation
- [ ] Unit tests

### Phase 2: Real-time Chat

- [ ] Tạo ChatMessage entity + migration
- [ ] Implement ChatHub
- [ ] Implement ChatService
- [ ] Configure SignalR trong Program.cs
- [ ] Frontend ChatBox component
- [ ] Test real-time communication

### Phase 3: LLM Suggest

- [ ] Configure OpenAI credentials
- [ ] Implement LlmService
- [ ] Create suggestion API endpoint
- [ ] Implement CannedResponses
- [ ] Frontend SuggestionPanel component

### Phase 4: RAG Chatbot

- [ ] Setup Vector Database
- [ ] Implement RagService
- [ ] Create RAG API endpoints
- [ ] Index Knowledge Base documents
- [ ] Test chatbot responses

---

## PHỤ LỤC

### A. Biến Môi Trường

```env
# .env.development
OPENAI_API_KEY=sk-your-key
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
AZURE_OPENAI_KEY=your-azure-key
QDRANT_HOST=localhost
QDRANT_PORT=6333
```

### B. Docker Compose (Optional)

```yaml
version: "3.8"
services:
  qdrant:
    image: qdrant/qdrant
    ports:
      - "6333:6333"
    volumes:
      - qdrant_data:/qdrant/storage

volumes:
  qdrant_data:
```

### C. Testing Commands

```powershell
# Test Sentiment API
Invoke-RestMethod -Uri "https://localhost:5001/api/ai/sentiment" `
  -Method POST `
  -Headers @{ Authorization = "Bearer $token" } `
  -Body (@{ text = "Ứng dụng lỗi liên tục!" } | ConvertTo-Json) `
  -ContentType "application/json"

# Test RAG Query
Invoke-RestMethod -Uri "https://localhost:5001/api/chat/rag/query" `
  -Method POST `
  -Body (@{ query = "Làm sao để reset mật khẩu?" } | ConvertTo-Json) `
  -ContentType "application/json"
```

---

> **Lưu ý:** Tài liệu này là hướng dẫn triển khai. Các chi tiết cụ thể có thể thay đổi tùy theo yêu cầu dự án.
