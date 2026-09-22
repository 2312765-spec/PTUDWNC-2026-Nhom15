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
    ICurrentUser currentUser)
    : IRequestHandler<UploadRecipeImageCommand, UploadRecipeImageResult>
{
    public async Task<UploadRecipeImageResult> Handle(UploadRecipeImageCommand request, CancellationToken cancellationToken)
    {
        var recipe = await recipeRepository.GetByIdWithImagesAsync(request.RecipeId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

        if (recipe.AuthorId != currentUser.UserId && !currentUser.IsAdmin)
        {
            throw new ForbiddenException(ErrorCodes.RecipeForbidden, "Bạn không có quyền sửa ảnh của công thức này.");
        }

        var url = await fileStorageService.UploadAsync(
            request.Content, request.FileName, request.ContentType, $"recipes/{recipe.Id}", cancellationToken);

        var image = recipe.AttachImage(url, request.AltText);

        // D31: PHẢI Add() tường minh — Recipe đã được track (Unchanged), nên EF Core không tự
        // biết ảnh mới thêm vào collection là INSERT hay UPDATE (Id là Guid gán sẵn ở
        // constructor, không phải giá trị CLR default), dễ đoán nhầm thành UPDATE → 0 dòng bị
        // ảnh hưởng → DbUpdateConcurrencyException giả. Chỉ dựa vào navigation fixup là không đủ.
        await imageRepository.AddAsync(image, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        request.TagsToInvalidate = ["recipes", $"recipe:{recipe.Slug}"];

        return new UploadRecipeImageResult(image.Id, image.OriginalUrl, image.AltText, image.IsPrimary);
    }
}
