using CulinaryBlog.API.Extensions;

namespace CulinaryBlog.API.Endpoints;

/// <summary>
/// Module Danh mục — SRS mục 8.2. Chủ sở hữu: <b>B</b>. Slice S3.
///
/// Quyết định bắt buộc: D8 (Redis, categories:all TTL 30p, invalidate CẢ tag "recipes"),
/// D2 (soft delete), D10 (slug auto-suffix).
/// </summary>
public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/categories").WithTags("Categories");

        group.MapGet("/", () => NotImplementedResults.Pending("FR-CAT-001", "B"))
             .WithSummary("Danh sách danh mục kèm recipeCount — cache 30 phút");

        group.MapGet("/{slug}", (string slug) => NotImplementedResults.Pending("FR-CAT-002", "B"))
             .WithSummary("Chi tiết danh mục + recipes phân trang");

        group.MapPost("/", () => NotImplementedResults.Pending("FR-CAT-003", "B"))
             .RequireAuthorization(Policies.Admin)
             .WithSummary("[Admin] Tạo danh mục — slug trùng thì auto-suffix (D10)");

        group.MapPut("/{id:guid}", (Guid id) => NotImplementedResults.Pending("FR-CAT-004", "B"))
             .RequireAuthorization(Policies.Admin)
             .WithSummary("[Admin] Cập nhật danh mục — Slug KHÔNG đổi khi đổi Name");

        group.MapDelete("/{id:guid}", (Guid id) => NotImplementedResults.Pending("FR-CAT-005", "B"))
             .RequireAuthorization(Policies.Admin)
             .WithSummary("[Admin] Soft delete — còn recipe thì 409 (D2)");
    }
}
