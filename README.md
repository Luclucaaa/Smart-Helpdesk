# Smart-Helpdesk

A centralized smart helpdesk system using ASP.NET Core MVC and ML.NET for multi-product support and automated sentiment analysis.

## Yêu cầu hệ thống

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (cho Backend)
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (cho Frontend)
- [MySQL Server](https://dev.mysql.com/downloads/mysql/) (phiên bản 8.0 trở lên)
- IDE: Visual Studio 2022 / VS Code / Rider

## Cài đặt và Cấu hình

### 1. Clone dự án

```bash
git clone https://github.com/Luclucaaa/Smart-Helpdesk.git
cd Smart-Helpdesk
```

### 2. Cấu hình Database

1. Cài đặt MySQL Server và đảm bảo service đang chạy
2. Tạo database mới (hoặc để EF Core tự tạo):
   ```sql
   CREATE DATABASE smarthelpdeskdb;
   ```
3. Cập nhật connection string trong `Backend/appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Port=3306;Database=smarthelpdeskdb;User=root;Password=YOUR_PASSWORD;"
   }
   ```

### 3. Chạy Backend

```bash
# Di chuyển vào thư mục Backend
cd Backend

# Restore packages
dotnet restore

# Chạy migrations để tạo database schema
dotnet ef database update

# Chạy ứng dụng Backend
dotnet run
```

Backend sẽ chạy tại: `http://localhost:5001` (hoặc theo cấu hình trong `launchSettings.json`)

Swagger UI có thể truy cập tại: `http://localhost:5001/swagger`

### 4. Chạy Frontend

Mở terminal mới:

```bash
# Di chuyển vào thư mục Frontend
cd Frontend

# Restore packages
dotnet restore

# Chạy ứng dụng Frontend
dotnet run
```

Frontend sẽ chạy tại: `http://localhost:5000` (hoặc theo cấu hình)

## Chạy với Visual Studio

1. Mở file `Smart-Helpdesk.sln` bằng Visual Studio 2022
2. Click chuột phải vào Solution > **Set Startup Projects**
3. Chọn **Multiple startup projects** và set cả Backend và Frontend là **Start**
4. Nhấn **F5** hoặc click **Start** để chạy cả hai dự án

## Cấu trúc dự án

```
Smart-Helpdesk/
├── Backend/                    # ASP.NET Core Web API
│   ├── Controllers/            # API Controllers
│   ├── Data/                   # DbContext và Entities
│   ├── DTOs/                   # Data Transfer Objects
│   ├── Interfaces/             # Service Interfaces
│   ├── Services/               # Business Logic
│   ├── Migrations/             # EF Core Migrations
│   └── appsettings.json        # Cấu hình Backend
│
├── Frontend/                   # Blazor WebAssembly
│   ├── Pages/                  # Razor Pages
│   ├── Layout/                 # Layout components
│   ├── Services/               # HTTP Services
│   └── wwwroot/                # Static files
│
└── Smart-Helpdesk.sln          # Solution file
```

## Tính năng chính

- Quản lý Tickets (tạo, cập nhật, theo dõi)
- Quản lý sản phẩm và danh mục sản phẩm
- Hệ thống comment trong tickets
- Đính kèm file
- Xác thực JWT
- Phân quyền người dùng

## Troubleshooting

### Lỗi kết nối MySQL

- Đảm bảo MySQL service đang chạy
- Kiểm tra port 3306 không bị block
- Xác nhận username/password đúng

### Lỗi Migration

```bash
# Nếu cần tạo lại database
dotnet ef database drop --force
dotnet ef database update
```

### Lỗi CORS

Đảm bảo Backend đã cấu hình CORS cho phép Frontend domain trong `Program.cs`
