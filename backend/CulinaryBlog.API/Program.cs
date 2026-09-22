using System.Text;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.API.Extensions;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.Application;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Seeding;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;

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

// ---- Hangfire worker (FR-JOB-001) -----------------------------------------
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHangfireServer();
}

// ---- ICurrentUser (hợp đồng chung — chủ sở hữu: A) -----------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// ---- Xác thực JWT (CONS-004) ---------------------------------------------
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
// ---- OpenAPI / Scalar (NFR-MAINT-003) ------------------------------------
// XÓA DÒNG builder.Services.AddOpenApi(); THỪA ĐI! Chỉ giữ lại 1 cấu hình chuẩn này:
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "Culinary Blog API",
            Version = "v1",
            Description = "API documentation with JWT Bearer Authentication"
        };

        // 1. Khai báo Bearer SecurityScheme
        document.Components ??= new Microsoft.OpenApi.OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes["bearerAuth"] = new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Description = "Nhập JWT Bearer token vào đây."
        };

        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var hasAuth = metadata.Any(m => m is Microsoft.AspNetCore.Authorization.IAuthorizeData);
        var allowAnonymous = metadata.Any(m => m is Microsoft.AspNetCore.Authorization.IAllowAnonymous);

        if (hasAuth && !allowAnonymous)
        {
            // BẮT BUỘC: Thêm Requirement vào operation
            operation.Security = new List<Microsoft.OpenApi.OpenApiSecurityRequirement>
            {
                new()
                {
                    [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("bearerAuth")] = new List<string>()
                }
            };
        }
        else
        {
            // Endpoint Guest/Public thì không có SecurityRequirement
            operation.Security = null;
        }

        return Task.CompletedTask;
    });
});

// ---- Health checks (FR-OBS-001) ------------------------------------------
builder.Services.AddAppHealthChecks(builder.Configuration);

var app = builder.Build();

// ---- Migration + seed (chỉ Development — Sprint 0, B) ---------------------
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
    await db.Database.MigrateAsync();
    await IdentityRoleSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
    await DbSeeder.SeedAsync(db);
}

// ---------------------------------------------------------------------------
// Pipeline — THỨ TỰ QUAN TRỌNG, đừng đảo
// ---------------------------------------------------------------------------

// 1. CorrelationId
app.UseMiddleware<CorrelationIdMiddleware>();

// 2. RFC 7807 Global Exception Handling
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseHsts();
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

// ---- Endpoint groups ------------------------------------------------------
app.MapHealthEndpoints();

var api = app.MapGroup("/api/v1");
api.MapAuthEndpoints();       // A
api.MapCategoryEndpoints();   // B
api.MapRecipeEndpoints();     // B (queries) + C (commands)
api.MapImageEndpoints();      // D

app.Run();

public partial class Program;