using System.Text;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.API.Extensions;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.Application;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Repositories;
// ---------------------------------------------------------------------------
// Culinary Blog API — .NET 10 Minimal APIs (CONS-003: KHÔNG dùng MVC Controllers)
// Kiến trúc: Clean Architecture 4 tầng + CQRS/MediatR (CONS-001, CONS-002)
// ---------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// ---- Serilog (CONS-010, FR-OBS-002) --------------------------------------
builder.Host.UseSerilog((context, services, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName());

// ---- Tầng ứng dụng --------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
// ---- ICurrentUser (hợp đồng chung — chủ sở hữu: A) -----------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// ---- Xác thực JWT (CONS-004) ---------------------------------------------
// TODO(S2 — A): thêm ASP.NET Core Identity + Google OAuth (D9).
var jwtKey = builder.Configuration["Jwt:Key"];
if (!string.IsNullOrWhiteSpace(jwtKey))
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.Zero,
            };
        });
}

// NFR-SEC-006: KHÔNG hardcode chuỗi role trong endpoint. Dùng policy.
// D12: chỉ 2 policy — policy "VerifiedAuthor" đã bị bỏ khỏi v1.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Author, policy => policy.RequireRole(Roles.Author, Roles.Admin))
    .AddPolicy(Policies.Admin, policy => policy.RequireRole(Roles.Admin));

// ---- CORS (NFR-SEC-005: allowlist, KHÔNG dùng "*") -----------------------
const string CorsPolicy = "CulinaryBlogCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
    .WithHeaders("Content-Type", "Authorization", "X-Correlation-ID", "If-Match")
    .AllowCredentials()));

// ---- OpenAPI / Scalar (NFR-MAINT-003) ------------------------------------
builder.Services.AddOpenApi();

// ---- Health checks (FR-OBS-001) ------------------------------------------
builder.Services.AddAppHealthChecks(builder.Configuration);

// TODO(S12 — A): Rate limiting theo D15 — /auth/* 10/phút/IP, API 100/phút/IP, upload 5/phút/IP.

var app = builder.Build();

// ---- Migration + seed (chỉ Development — Sprint 0, B) ---------------------
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

// ---------------------------------------------------------------------------
// Pipeline — THỨ TỰ QUAN TRỌNG, đừng đảo
// ---------------------------------------------------------------------------

// 1. CorrelationId phải đứng trước mọi thứ để mọi log đều có nó (CONS-010).
app.UseMiddleware<CorrelationIdMiddleware>();

// 2. Bắt mọi exception chưa xử lý → RFC 7807 (CONS-005, D4).
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseHsts(); // NFR-SEC-005
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

// ---- Endpoint groups ------------------------------------------------------
HealthEndpoints.MapHealthEndpoints(app);

var api = app.MapGroup("/api/v1"); // CONS-005: version qua URL path
api.MapAuthEndpoints();       // A
api.MapCategoryEndpoints();   // B
api.MapRecipeEndpoints();     // B (queries) + C (commands)
api.MapImageEndpoints();      // D

app.Run();

/// <summary>Điểm vào của API — public để WebApplicationFactory trong IntegrationTests dùng được.</summary>
public partial class Program;
