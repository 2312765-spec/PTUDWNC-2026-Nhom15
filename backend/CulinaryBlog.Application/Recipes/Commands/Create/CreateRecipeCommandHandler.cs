using System.Text.RegularExpressions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Observability;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Commands.Create;

/// <summary>
/// FR-RCP-003 — tạo recipe Draft. D10/D72 slug auto-suffix (không bao giờ 409), D85 thử lại khi tạo song song, D39 metric
/// <c>recipes.created</c> sau khi lưu thành công, NFR-SEC-006 kiểm tra quyền ở Application.
/// </summary>
public sealed partial class CreateRecipeCommandHandler(
    IRecipeRepository recipeRepository,
    ICategoryRepository categoryRepository,
    ISlugHelper slugHelper,
    IIdentityService identityService,
    RecipeAuthorizationHandler authorization,
    ICurrentUser currentUser,
    RecipeMetrics metrics)
    : IRequestHandler<CreateRecipeCommand, RecipeDetailDto>
{
    /// <summary>D10 — slug trùng route của frontend/API.</summary>
    private static readonly string[] ReservedSlugs = ["search", "new", "edit"];

    /// <summary>D72 — title không còn ký tự a-z0-9 nào (vd. "!!!!!") vẫn phải có slug.</summary>
    public const string FallbackSlug = "cong-thuc";

    private const int MaxSlugLength = 200;

    /// <summary>D85 — N request cùng title đến cùng lúc thì request xui nhất thua tối đa N−1 vòng.</summary>
    public const int MaxSaveAttempts = 5;

    public async Task<RecipeDetailDto> Handle(CreateRecipeCommand request, CancellationToken cancellationToken)
    {
        authorization.EnsureCanCreate();

        // Validator đã kiểm tra; đọc lại để có tên category cho DTO. Nếu category bị xóa ngay giữa
        // hai bước thì vẫn trả 400 như validator (D69), không để FK lỗi thành 500.
        var category = await categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new ValidationException([new ValidationFailure(nameof(request.CategoryId), "Danh mục không hợp lệ.")]);

        var title = request.Title.Trim();

        // D85: kiểm tra slug rồi mới lưu không nguyên tử — request khác cùng title có thể chiếm slug giữa hai bước.
        // Thua thì sinh lại slug (lúc này thấy slug của bên thắng → -2, -3…) và dựng lại recipe.
        for (var attempt = 1; ; attempt++)
        {
            var slug = await GenerateUniqueSlugAsync(title, cancellationToken);
            var recipe = BuildRecipe(request, title, slug, category.Id);

            if (await recipeRepository.TryAddAsync(recipe, cancellationToken))
            {
                metrics.RecordCreated();

                var author = await identityService.GetUserByIdAsync(recipe.AuthorId, cancellationToken);
                return RecipeDetailDtoMapper.ToDetailDto(recipe, author, category);
            }

            if (attempt == MaxSaveAttempts)
            {
                throw new InvalidOperationException(
                    $"D85: không lưu được recipe sau {MaxSaveAttempts} lần — slug '{slug}' liên tục bị request khác chiếm.");
            }
        }
    }

    private Recipe BuildRecipe(CreateRecipeCommand request, string title, string slug, Guid categoryId)
    {
        var recipe = Recipe.Create(
            title: title,
            slug: slug,
            description: request.Description.Trim(),
            prepTime: request.PrepTime,
            cookTime: request.CookTime,
            servings: request.Servings,
            difficulty: RecipeDifficultyParser.Parse(request.Difficulty),
            categoryId: categoryId,
            authorId: currentUser.UserId!,
            instructions: string.IsNullOrWhiteSpace(request.Instructions) ? null : request.Instructions.Trim(),
            status: Domain.Enums.RecipeStatus.Draft);

        if (request.Nutrition is { } n)
        {
            recipe.SetNutrition(n.Calories, n.Protein, n.Carbohydrates, n.Fat, n.Fiber, n.Sodium);
        }

        foreach (var step in request.Steps ?? [])
        {
            recipe.AppendStep(step.Title.Trim(), step.Description.Trim(), step.TimerMinutes, NullIfBlank(step.ImageUrl));
        }

        foreach (var ingredient in request.Ingredients ?? [])
        {
            recipe.AddIngredient(ingredient.Name.Trim(), ingredient.Quantity, NullIfBlank(ingredient.Unit), NullIfBlank(ingredient.Notes));
        }

        return recipe;
    }

    /// <summary>
    /// D10 + D72: SlugHelper.Generate → gộp '-' liên tiếp, cắt 200 ký tự → slug cấm thì thêm "-1" →
    /// trùng (kể cả recipe đã xóa mềm, vì unique index phủ cả bản ghi đã xóa — D1) thì thêm "-2", "-3"…
    /// </summary>
    private async Task<string> GenerateUniqueSlugAsync(string title, CancellationToken ct)
    {
        var baseSlug = MultipleDashes().Replace(slugHelper.Generate(title), "-").Trim('-');
        if (baseSlug.Length > MaxSlugLength)
        {
            baseSlug = baseSlug[..MaxSlugLength].TrimEnd('-');
        }

        if (baseSlug.Length == 0)
        {
            baseSlug = FallbackSlug;
        }

        // D75: slug có dạng GUID sẽ bị route GET /recipes/{id:guid} bắt trước route /{slug}.
        if (ReservedSlugs.Contains(baseSlug) || Guid.TryParse(baseSlug, out _))
        {
            baseSlug += "-1";
        }

        var candidate = baseSlug;
        var suffix = 2;
        while (await recipeRepository.ExistsBySlugAsync(candidate, ct))
        {
            candidate = $"{baseSlug}-{suffix++}";
        }

        return candidate;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultipleDashes();
}
