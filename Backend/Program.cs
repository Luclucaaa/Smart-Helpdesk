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
        policy.WithOrigins("https://localhost:7042", "http://localhost:5130")
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
    string[] roleNames = { "Admin", "Nhân viên", "Customer" };
    
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new Role { Name = roleName });
        }
    }
    
    // Xóa role "Support" nếu tồn tại (đã đổi sang "Nhân viên")
    var supportRole = await roleManager.FindByNameAsync("Support");
    if (supportRole != null)
    {
        await roleManager.DeleteAsync(supportRole);
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
            await userManager.AddToRoleAsync(staffUser, "Nhân viên");
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
app.MapControllers();
app.Run();