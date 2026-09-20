using CulinaryBlog.API.Extensions;

namespace CulinaryBlog.API.Endpoints;

/// <summary>
/// Ảnh công thức — SRS mục 8.4. Chủ sở hữu: <b>D</b>. Slice S8.
///
/// D22 — BA endpoint, KHÔNG có /images/{imageId}/primary như SRS FR-RCP-008 viết.
/// CONS-007 + NFR-SEC-004 — thứ tự validation: size (trước khi đọc stream) → MIME → magic bytes.
/// </summary>
public static class ImageEndpoints
{
    public static void MapImageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/recipes/{id:guid}/images").WithTags("Recipe Images");

        group.MapPost("/", (Guid id) => NotImplementedResults.Pending("FR-RCP-008", "D"))
             .RequireAuthorization(Policies.Author)
             .DisableAntiforgery()
             .WithSummary("Upload ảnh — multipart: file, altText?. Ảnh đầu tiên tự động primary");

        group.MapPatch("/{imageId:guid}", (Guid id, Guid imageId) => NotImplementedResults.Pending("FR-RCP-008", "D"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Sửa metadata — { altText?, isPrimary?, orderIndex? } (D22)");

        group.MapDelete("/{imageId:guid}", (Guid id, Guid imageId) => NotImplementedResults.Pending("FR-RCP-008", "D"))
             .RequireAuthorization(Policies.Author)
             .WithSummary("Xóa ảnh — xóa file MinIO qua Hangfire. Primary bị xóa thì ảnh kế lên thay");
    }
}
