using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Recipes.Commands.Images;

/// <summary>
/// FR-JOB-002/D40–D44, D88 — sinh medium + thumbnail + card cho một ảnh đã upload. Idempotent: mọi trường hợp
/// "không còn gì để làm" (recipe đã soft delete, ảnh đã xóa, đã có card, định dạng không
/// resize được) kết thúc êm, không ném lỗi — Hangfire retry những trường hợp đó là vô ích.
/// Không kiểm tra ownership: lệnh hệ thống, không có endpoint gọi tới.
/// </summary>
public sealed class GenerateRecipeImageVariantsCommandHandler(
    IRecipeRepository recipeRepository,
    IFileStorageService fileStorageService,
    IImageResizer imageResizer,
    IUnitOfWork unitOfWork,
    IBackgroundJobService backgroundJobService,
    ILogger<GenerateRecipeImageVariantsCommandHandler> logger)
    : IRequestHandler<GenerateRecipeImageVariantsCommand>
{
    public async Task Handle(GenerateRecipeImageVariantsCommand request, CancellationToken cancellationToken)
    {
        // Global query filter + điều kiện !IsDeleted trong repository → recipe đã soft delete trả null (D1).
        var recipe = await recipeRepository.GetByIdWithImagesAsync(request.RecipeId, cancellationToken);
        var image = recipe?.Images.FirstOrDefault(i => i.Id == request.ImageId);
        if (recipe is null || image is null)
        {
            logger.LogInformation(
                "FR-JOB-002: bỏ qua resize — recipe {RecipeId} hoặc ảnh {ImageId} không còn tồn tại",
                request.RecipeId, request.ImageId);
            return;
        }

        // D88: đủ 3 biến thể mới là xong. Ảnh xử lý trước D88 có medium/thumbnail nhưng chưa có card → chạy tiếp,
        // chỉ upload card (không tạo lại medium/thumbnail, không sinh file mồ côi).
        if (image.CardUrl is not null)
        {
            return;
        }

        var hasMediumAndThumbnail = image.MediumUrl is not null && image.ThumbnailUrl is not null;

        ResizedImageSet? resized;
        await using (var original = await fileStorageService.DownloadAsync(image.OriginalUrl, cancellationToken))
        {
            resized = imageResizer.Resize(original);
        }

        if (resized is null)
        {
            logger.LogInformation(
                "FR-JOB-002/D41: ảnh {ImageId} không resize được (định dạng không hỗ trợ hoặc quá lớn) — giữ ảnh gốc",
                request.ImageId);
            return;
        }

        var folder = $"recipes/{request.RecipeId}";
        var uploadedUrls = new List<string>(3);
        try
        {
            string mediumUrl, thumbnailUrl;
            if (hasMediumAndThumbnail)
            {
                mediumUrl = image.MediumUrl!;
                thumbnailUrl = image.ThumbnailUrl!;
            }
            else
            {
                mediumUrl = await UploadAsync(resized.Medium, "medium.webp", folder, cancellationToken);
                uploadedUrls.Add(mediumUrl);
                thumbnailUrl = await UploadAsync(resized.Thumbnail, "thumbnail.webp", folder, cancellationToken);
                uploadedUrls.Add(thumbnailUrl);
            }

            var cardUrl = await UploadAsync(resized.Card, "card.webp", folder, cancellationToken);
            uploadedUrls.Add(cardUrl);

            recipe.SetImageVariants(image.Id, mediumUrl, thumbnailUrl, cardUrl);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // D43/D44: file đã lên MinIO nhưng URL không vào được DB (ảnh bị xóa giữa chừng, conflict,
            // DB lỗi) → dọn để không thành file mồ côi; ném lại để Hangfire retry theo D42.
            foreach (var url in uploadedUrls)
            {
                backgroundJobService.EnqueueDeleteImageFile(url);
            }

            throw;
        }

        request.TagsToInvalidate = ["recipes", $"recipe:{recipe.Slug}"];
    }

    private async Task<string> UploadAsync(byte[] content, string fileName, string folder, CancellationToken ct)
    {
        using var stream = new MemoryStream(content, writable: false);
        return await fileStorageService.UploadAsync(stream, fileName, IImageResizer.OutputContentType, folder, ct);
    }
}
