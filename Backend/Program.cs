using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FluentValidation;
using SmartHelpdesk.Data;
using SmartHelpdesk.Data.Entities;
using SmartHelpdesk.Interfaces;
using SmartHelpdesk.Services;
using SmartHelpdesk.Common.Mappings;
using SmartHelpdesk.Validators;
using SmartHelpdesk.DTOs.Requests;

var builder = WebApplication.CreateBuilder(args);

// Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SmartHelpdeskContext>(options =>
    options.UseMySql(connectionString!, ServerVersion.AutoDetect(connectionString!))
);

// Identity Configuration
builder.Services.AddIdentity<User, Role>()
    .AddEntityFrameworkStores<SmartHelpdeskContext>()
    .AddDefaultTokenProviders();

// JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!))
    };
});

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy.WithOrigins(
                "https://localhost:7042", 
                "http://localhost:5130",
                "http://localhost:5000",
                "http://localhost:5002"  // Frontend Blazor WASM
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// AutoMapper
builder.Services.AddAutoMapper(typeof(SmartHelpdeskProfile));

// FluentValidation
builder.Services.AddScoped<IValidator<UserLoginDTO>, LoginValidator>();
builder.Services.AddScoped<IValidator<UserRegistrationDTO>, RegistrationValidator>();
builder.Services.AddScoped<IValidator<CreateTicketDTO>, CreateTicketValidator>();
builder.Services.AddScoped<IValidator<UpdateTicketDTO>, UpdateTicketValidator>();
builder.Services.AddScoped<IValidator<CreateCommentDTO>, CreateCommentValidator>();
builder.Services.AddScoped<IValidator<UpdateCommentDTO>, UpdateCommentValidator>();

// Services
builder.Services.AddScoped<ITicketsService, TicketsService>();
builder.Services.AddScoped<ICommentsService, CommentsService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// AI Services
builder.Services.AddSingleton<ISentimentService, SentimentService>();

// Đăng ký GeminiService với DI, lấy API key từ cấu hình
builder.Services.AddSingleton<GeminiService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var apiKey = config["Gemini:ApiKey"] ?? "YOUR_GEMINI_API_KEY";
    var model = config["Gemini:Model"] ?? "gemini-2.5-flash";
    return new GeminiService(apiKey, model);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Seed Roles và Users mặc định
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

    // Seed Roles
    string[] roleNames = { "Admin", "Agent", "Customer" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new Role { Name = roleName });
        }
    }

    // Xóa hoàn toàn role "Nhân viên" và "Support"
    var nhanvienRole = await roleManager.FindByNameAsync("Nhân viên");
    if (nhanvienRole != null)
    {
        var usersInOldRole = await userManager.GetUsersInRoleAsync("Nhân viên");
        foreach (var u in usersInOldRole)
        {
            await userManager.RemoveFromRoleAsync(u, "Nhân viên");
            await userManager.AddToRoleAsync(u, "Agent");
        }
        await roleManager.DeleteAsync(nhanvienRole);
    }
    var supportRole = await roleManager.FindByNameAsync("Support");
    if (supportRole != null)
    {
        var usersInOldRole = await userManager.GetUsersInRoleAsync("Support");
        foreach (var u in usersInOldRole)
        {
            await userManager.RemoveFromRoleAsync(u, "Support");
            await userManager.AddToRoleAsync(u, "Agent");
        }
        await roleManager.DeleteAsync(supportRole);
    }

    // Đảm bảo mọi user đều có ít nhất 1 role (nếu chưa có thì gán Customer)
    var allUsers = userManager.Users.ToList();
    foreach (var user in allUsers)
    {
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count == 0)
        {
            await userManager.AddToRoleAsync(user, "Customer");
        }
    }

    // Seed Admin account
    var adminEmail = "admin@smarthelpdesk.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new User
        {
            UserName = "admin",
            Email = adminEmail,
            Name = "Quản trị",
            Surname = "Admin",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(adminUser, "Admin@123");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
    else
    {
        var roles = await userManager.GetRolesAsync(adminUser);
        if (!roles.Contains("Admin"))
        {
            foreach (var r in roles) await userManager.RemoveFromRoleAsync(adminUser, r);
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }

    // Seed Nhân viên account
    var staffEmail = "nhanvien@smarthelpdesk.com";
    var staffUser = await userManager.FindByEmailAsync(staffEmail);
    if (staffUser == null)
    {
        staffUser = new User
        {
            UserName = "nhanvien",
            Email = staffEmail,
            Name = "Nhân viên",
            Surname = "Hỗ trợ",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(staffUser, "Nhanvien@123");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(staffUser, "Agent");
        }
    }
    else
    {
        var roles = await userManager.GetRolesAsync(staffUser);
        if (!roles.Contains("Agent"))
        {
            foreach (var r in roles) await userManager.RemoveFromRoleAsync(staffUser, r);
            await userManager.AddToRoleAsync(staffUser, "Agent");
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowBlazorClient");

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Đảm bảo wwwroot và attachments folder tồn tại trước khi serve static files
var webRootPath = app.Environment.WebRootPath;
if (string.IsNullOrEmpty(webRootPath))
{
    webRootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    app.Environment.WebRootPath = webRootPath;
}
Directory.CreateDirectory(Path.Combine(webRootPath, "attachments"));

app.UseStaticFiles();
app.MapControllers();
app.Run();