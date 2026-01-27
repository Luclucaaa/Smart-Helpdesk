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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();