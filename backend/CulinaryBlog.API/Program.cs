using System.Text;
using CulinaryBlog.Application;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.WebApi.Middleware;
using CulinaryBlog.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình ASP.NET Core .NET 10 Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

// 2. Cấu hình IMemoryCache (SRS FR-CAT-001 TTL 60 phút sliding expiration)
builder.Services.AddMemoryCache();

// 3. Đăng ký Application Services (MediatR, FluentValidation, ValidationBehavior)
builder.Services.AddApplicationServices();

// 4. Đăng ký Infrastructure Services (EF Core DbContext, Repositories, UnitOfWork)
builder.Services.AddInfrastructureServices(builder.Configuration);

// 5. Đăng ký Current User Service
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// 6. Cấu hình Authentication & Authorization (JWT Bearer với Role Admin / Author)
var jwtKey = builder.Configuration["Jwt:Key"] ?? "CulinaryBlogSuperSecretKey2026Net10AspNetCoreAuthenticationKey!";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireAuthor", policy => policy.RequireRole("Author", "Admin"));
});

// 7. Cấu hình Swagger / OpenAPI Documentation
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Culinary Blog Category API (.NET 10)",
        Version = "v1.0.0",
        Description = "API Module Quản lý Danh mục Ẩm thực theo tài liệu SRS v1.0.0 (FR-CAT-001 đến FR-CAT-005)"
    });

    // 1. Khắc phục lỗi SchemaId trùng lặp giữa các DTOs / Records / ProblemDetails
    c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

    // 2. Khắc phục lỗi CS0029 & lỗi 500: Loại bỏ tham số CancellationToken khỏi Swagger UI
    c.MapType<CancellationToken>(() => new OpenApiSchema());
    c.OperationFilter<IgnoreCancellationTokenOperationFilter>();

    // 3. Xử lý giải quyết xung đột action routes nếu có
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

    // 4. Cấu hình xác thực JWT Bearer trên Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập JWT Bearer Token theo định dạng: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

  c.AddSecurityRequirement(new OpenApiSecurityRequirement
{
    {
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        },
        Array.Empty<string>()
    }
});
});

var app = builder.Build();

// 8. Tự động Seed dữ liệu mẫu ban đầu cho Database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (!context.Categories.Any())
    {
        var cat1 = Category.Create("Món Chính", "mon-chinh", "Các món ăn chính thơm ngon, đậm đà cho bữa cơm gia đình.", orderIndex: 1);
        var cat2 = Category.Create("Món Canh", "mon-canh", "Canh ngọt thanh mát giải nhiệt ngày hè, ấm lòng ngày đông.", orderIndex: 2);
        var cat3 = Category.Create("Bún & Phở", "bun-pho", "Tinh hoa ẩm thực truyền thống nước lèo phở bò, bún chả...", orderIndex: 3);
        var cat4 = Category.Create("Tráng Miệng", "trang-mieng", "Các món chè bưởi, bánh ngọt, trái cây thanh mát.", orderIndex: 4);

        context.Categories.AddRange(cat1, cat2, cat3, cat4);

        var r1 = Recipe.Create("Phở Bò Tái Lăn Hà Nội", "pho-bo-tai-lan", "Bí quyết nước dùng thanh ngọt", cat3.Id, "usr-chef-1", "Chef Minh Tuấn", null, RecipeStatus.Published, "Medium", 30, 180, 4, "https://images.unsplash.com/photo-1582878826629-29b7ad1cdc43?w=800");
        var r2 = Recipe.Create("Cá Kho Tộ Miền Tây", "ca-kho-to", "Cá bống kho tộ nước màu dừa", cat1.Id, "usr-chef-1", "Chef Minh Tuấn", null, RecipeStatus.Published, "Easy", 15, 45, 4, "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=800");
        var r3 = Recipe.Create("Canh Chua Cá Hồi", "canh-chua-ca-hoi", "Vị chua thanh mát mùa hè", cat2.Id, "usr-chef-2", "Bếp Trưởng Hoàng Oanh", null, RecipeStatus.Published, "Easy", 15, 20, 4, "https://images.unsplash.com/photo-1547592166-23ac45744acd?w=800");
        var r4 = Recipe.Create("Bò Kho Nước Dừa (Bản Nháp)", "bo-kho-nhap", "Công thức thử nghiệm", cat1.Id, "usr-chef-1", "Chef Minh Tuấn", null, RecipeStatus.Draft, "Medium", 20, 60, 4, "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=800");

        context.Recipes.AddRange(r1, r2, r3, r4);
        context.SaveChanges();
    }
}

// 9. Pipeline Middleware
app.UseMiddleware<ProblemDetailsExceptionMiddleware>();

// Cấu hình Swagger & Swagger UI (luôn bật để kiểm thử API các chức năng FR-CAT-001 đến FR-CAT-005)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Culinary Blog Category API v1 (.NET 10)");
    c.RoutePrefix = "swagger"; // Truy cập UI tại /swagger
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>
/// Filter tự động ẩn tham số CancellationToken khỏi Swagger UI để tránh lỗi serialize WaitHandle
/// </summary>
public class IgnoreCancellationTokenOperationFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    public void Apply(OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
    {
        var ctParams = context.ApiDescription.ParameterDescriptions
            .Where(p => p.ModelMetadata?.ModelType == typeof(CancellationToken) || p.Type == typeof(CancellationToken))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (ctParams.Count > 0 && operation.Parameters != null)
        {
            var parametersToRemove = operation.Parameters
                .Where(p => ctParams.Contains(p.Name))
                .ToList();

            foreach (var parameter in parametersToRemove)
            {
                operation.Parameters.Remove(parameter);
            }
        }
    }
}
