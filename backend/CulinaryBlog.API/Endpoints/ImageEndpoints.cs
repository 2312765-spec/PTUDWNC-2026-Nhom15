using CulinaryBlog.API.Extensions;
using CulinaryBlog.Application.Recipes.Commands.Images;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBlog.API.Endpoints;

/// <summary>
/// Ảnh công thức — SRS mục 8.4. Chủ sở hữu: D. Slice S8.
/// D22 — BA endpoint, KHÔNG có /images/{imageId}/primary như SRS FR-RCP-008 viết.
/// CONS-007 + NFR-SEC-004 — thứ tự validation: size (trước khi đọc stream) → MIME → magic bytes.
/// </summary>
public static class ImageEndpoints
{
    public static void MapImageEndpoints(this IEndpointRouteBuilder app)
    {
        // SỬA LỖI 404: Thêm tiền tố /api/v1 vào trước route
        var group = app.MapGroup("/api/v1/recipes/{id:guid}/images").WithTags("Recipe Images");

        group.MapPost("/", UploadAsync)
             .RequireAuthorization(Policies.Author) // Bắt buộc đăng nhập (chưa login -> 401)
             .DisableAntiforgery()
             .WithSummary("Upload ảnh — multipart: file, altText?. Ảnh đầu tiên tự động primary");

        group.MapPatch("/{imageId:guid}", UpdateAsync)
             .RequireAuthorization(Policies.Author)
             .WithSummary("Sửa metadata — { altText?, isPrimary?, orderIndex? } (D22)");

        group.MapDelete("/{imageId:guid}", DeleteAsync)
             .RequireAuthorization(Policies.Author)
             .WithSummary("Xóa ảnh — xóa file MinIO qua Hangfire. Primary bị xóa thì ảnh kế lên thay");
    }

    /// <summary>CONS-008: endpoint chỉ nhận request → gửi command → trả kết quả. Không validate ở đây.</summary>
    private static async Task<IResult> UploadAsync(
        Guid id,
        IFormFile file,
        [FromForm] string? altText,
        ISender sender,
        CancellationToken ct)
    {
        var command = new UploadRecipeImageCommand(
            id, file.OpenReadStream(), file.Length, file.ContentType, file.FileName, altText);

        var result = await sender.Send(command, ct);

        return TypedResults.Created((string?)null, result);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        Guid imageId,
        UpdateImageRequest request,
        ISender sender,
        CancellationToken ct)
    {
        var command = new UpdateRecipeImageCommand(id, imageId, request.AltText, request.IsPrimary, request.OrderIndex);
        await sender.Send(command, ct);
        return TypedResults.Ok();
    }

    private static async Task<IResult> DeleteAsync(Guid id, Guid imageId, ISender sender, CancellationToken ct)
    {
        await sender.Send(new DeleteRecipeImageCommand(id, imageId), ct);
        return TypedResults.NoContent();
    }
}

/// <summary>D22/D27 — wire contract của PATCH /recipes/{id}/images/{imageId}.</summary>
public sealed record UpdateImageRequest(string? AltText, bool? IsPrimary, int? OrderIndex);