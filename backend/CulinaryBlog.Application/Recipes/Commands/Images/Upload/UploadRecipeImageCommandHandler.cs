using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Images;

/// <summary>
/// FR-RCP-008/D22/D27 — quyền chỉ phụ thuộc ownership (Owner/Admin), không phụ thuộc
/// Recipe.Status (sửa ảnh được ở mọi status).
/// </summary>
public sealed class UploadRecipeImageCommandHandler(
    IRecipeRepository recipeRepository,
    IRepository<RecipeImage> imageRepository,
    IFileStorageService fileStorageService,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IBackgroundJobService backgroundJobService)
    : IRequestHandler<UploadRecipeImageCommand, UploadRecipeImageResult>
{
    public async Task<UploadRecipeImageResult> Handle(UploadRecipeImageCommand request, CancellationToken cancellationToken)
    {
        // Kiểm tra quyền TRƯỚC khi upload: không tốn băng thông MinIO cho request sẽ bị từ chối.
        var authorId = await recipeRepository.GetAuthorIdAsync(request.RecipeId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

        if (authorId != currentUser.UserId && !currentUser.IsAdmin)
        {
            throw new ForbiddenException(ErrorCodes.RecipeForbidden, "Bạn không có quyền sửa ảnh của công thức này.");
        }

        // Upload NGOÀI transaction — không giữ khoá dòng Recipe trong lúc chờ mạng.
        var url = await fileStorageService.UploadAsync(
            request.Content, request.FileName, request.ContentType, $"recipes/{request.RecipeId}", cancellationToken);

        try
        {
            UploadRecipeImageResult? result = null;
            var recipeSlug = string.Empty;

            // Khoá dòng Recipe rồi mới quyết định primary/orderIndex: nhiều upload đồng thời vào cùng
            // một recipe phải xếp hàng, nếu không tất cả cùng thấy "chưa có ảnh" → cùng primary → 500.
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var recipe = await recipeRepository.GetByIdWithImagesForUpdateAsync(request.RecipeId, ct)
                    ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

                var image = recipe.AttachImage(url, request.AltText);

                // D31: PHẢI Add() tường minh — Recipe đã được track (Unchanged), nên EF Core không tự
                // biết ảnh mới thêm vào collection là INSERT hay UPDATE (Id là Guid gán sẵn ở
                // constructor, không phải giá trị CLR default), dễ đoán nhầm thành UPDATE → 0 dòng bị
                // ảnh hưởng → DbUpdateConcurrencyException giả. Chỉ dựa vào navigation fixup là không đủ.
                await imageRepository.AddAsync(image, ct);
                await unitOfWork.SaveChangesAsync(ct);

                recipeSlug = recipe.Slug;
                result = new UploadRecipeImageResult(image.Id, image.OriginalUrl, image.AltText, image.IsPrimary);
            }, cancellationToken);

            request.TagsToInvalidate = ["recipes", $"recipe:{recipeSlug}"];

            return result!;
        }
        catch
        {
            // File đã lên MinIO nhưng DB không ghi được → xoá bất đồng bộ để khỏi thành file mồ côi.
            backgroundJobService.EnqueueDeleteImageFile(url);
            throw;
        }
    }
}
