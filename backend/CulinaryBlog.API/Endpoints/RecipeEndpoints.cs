using CulinaryBlog.API.Extensions;

namespace CulinaryBlog.API.Endpoints;

/// <summary>
/// Module Công thức — SRS mục 8.3. Chủ sở hữu: <b>B</b> (queries) + <b>C</b> (commands).
///
/// ⚠️ THỨ TỰ ĐĂNG KÝ ROUTE QUAN TRỌNG (D10):
/// "/search" phải đăng ký TRƯỚC "/{slug}", nếu không request tới /recipes/search
/// sẽ khớp vào route slug. SlugHelper cũng cấm sinh slug "search".
/// </summary>
public static class RecipeEndpoints
{
    public static void MapRecipeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/recipes").WithTags("Recipes");

        // ---- B: queries ----
        group.MapGet("/", () => NotImplementedResults.Pending("FR-RCP-001", "B"))
             .WithSummary("Danh sách — authorization filter + filter/sort/paging (FR-SRCH-002/003/004)");

        // PHẢI đứng trước /{slug}
        group.MapGet("/search", () => NotImplementedResults.Pending("FR-SRCH-001", "B"))
             .WithSummary("Full-text search tiếng Việt — ?q= (tối thiểu 2 ký tự)");

        group.MapGet("/{slug}", (string slug) => NotImplementedResults.Pending("FR-RCP-002", "B"))
             .WithSummary("Chi tiết theo slug — Draft/Archived: chỉ owner hoặc Admin (403)");

        // ---- C: commands ----
        group.MapPost("/", () => NotImplementedResults.Pending("FR-RCP-003", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Tạo công thức (Status = Draft) — slug auto-suffix (D10)");

        group.MapPut("/{id:guid}", (Guid id) => NotImplementedResults.Pending("FR-RCP-004", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Cập nhật — RowVersion mismatch → 409 (D4)");

        group.MapPatch("/{id:guid}/publish", (Guid id) => NotImplementedResults.Pending("FR-RCP-005", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Publish — cần ≥1 step VÀ ≥1 ingredient (D3)");

        group.MapPatch("/{id:guid}/unpublish", (Guid id) => NotImplementedResults.Pending("FR-RCP-005", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Unpublish — Published → Draft");

        group.MapPatch("/{id:guid}/archive", (Guid id) => NotImplementedResults.Pending("FR-RCP-006", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Archive — ẩn khỏi listing công khai");

        group.MapDelete("/{id:guid}", (Guid id) => NotImplementedResults.Pending("FR-RCP-007", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("SOFT DELETE (D1) — không cascade, không xóa file MinIO");

        // ---- C: steps & ingredients ----
        group.MapPost("/{id:guid}/steps", (Guid id) => NotImplementedResults.Pending("FR-RCP-010", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Thêm bước — server sinh stepNumber, body KHÔNG có stepNumber (D6)");

        group.MapPut("/{id:guid}/steps/{stepId:guid}", (Guid id, Guid stepId) => NotImplementedResults.Pending("FR-RCP-010", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Sửa bước — stepNumber? để đổi vị trí, server renumber lại");

        group.MapDelete("/{id:guid}/steps/{stepId:guid}", (Guid id, Guid stepId) => NotImplementedResults.Pending("FR-RCP-010", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Xóa bước — renumber lại cho liên tục 1,2,3…");

        group.MapPost("/{id:guid}/ingredients", (Guid id) => NotImplementedResults.Pending("FR-RCP-009", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Thêm nguyên liệu — quantity/unit NULLABLE (D7)");

        group.MapPut("/{id:guid}/ingredients/{ingredientId:guid}", (Guid id, Guid ingredientId) => NotImplementedResults.Pending("FR-RCP-009", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Sửa nguyên liệu");

        group.MapDelete("/{id:guid}/ingredients/{ingredientId:guid}", (Guid id, Guid ingredientId) => NotImplementedResults.Pending("FR-RCP-009", "C"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Xóa nguyên liệu");
    }
}
