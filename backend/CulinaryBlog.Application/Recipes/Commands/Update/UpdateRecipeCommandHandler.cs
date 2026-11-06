using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Interfaces;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Update;

/// <summary>
/// FR-RCP-004 — thứ tự kiểm tra: 404 (A3) → 403 (A1, NFR-SEC-006) → 409 (A2). Quyền đứng trước concurrency
/// để người không có quyền không dò được RowVersion qua 409. Hai request chen nhau giữa lúc đọc và lúc lưu
/// do RowVersionInterceptor chặn (DbUpdateConcurrencyException → 409, D34).
/// </summary>
public sealed class UpdateRecipeCommandHandler(
    IRecipeRepository recipeRepository,
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    IIdentityService identityService,
    RecipeAuthorizationHandler authorization)
    : IRequestHandler<UpdateRecipeCommand, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(UpdateRecipeCommand request, CancellationToken cancellationToken)
    {
        var recipe = await recipeRepository.GetByIdWithDetailsAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.RecipeNotFound, "Không tìm thấy công thức.");

        authorization.EnsureCanModify(recipe);

        // D34/D71: client sửa trên bản đã cũ (đọc từ trước khi người khác lưu) → 409.
        if (!RowVersionHeader.TryParse(request.IfMatch, out var expected) || !expected.AsSpan().SequenceEqual(recipe.RowVersion))
        {
            throw new ConflictException(
                ErrorCodes.RecipeConcurrencyConflict,
                "Dữ liệu đã bị thay đổi bởi người dùng khác. Tải lại công thức rồi sửa tiếp.");
        }

        var category = await categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new ValidationException([new ValidationFailure(nameof(request.CategoryId), "Danh mục không hợp lệ.")]);

        recipe.Update(
            request.Title.Trim(),
            request.Description.Trim(),
            string.IsNullOrWhiteSpace(request.Instructions) ? null : request.Instructions.Trim(),
            request.PrepTime,
            request.CookTime,
            request.Servings,
            RecipeDifficultyParser.Parse(request.Difficulty),
            category.Id);

        // FR-RCP-004 bước 8 + D69: không gửi nutrition = giữ nguyên.
        if (request.Nutrition is { } n)
        {
            recipe.SetNutrition(n.Calories, n.Protein, n.Carbohydrates, n.Fat, n.Fiber, n.Sodium);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        request.TagsToInvalidate = ["recipes", $"recipe:{recipe.Slug}"];

        var author = await identityService.GetUserByIdAsync(recipe.AuthorId, cancellationToken);
        return RecipeDetailDtoMapper.ToDetailDto(recipe, author, category);
    }
}
