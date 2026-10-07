namespace CulinaryBlog.Application.Recipes.DTOs;

public sealed record RecipeDetailDto(
    Guid Id,
    string Title,
    string Slug,
    string Description,
    string? Instructions,
    short Difficulty,
    short Status,
    int PrepTime,
    int CookTime,
    int Servings,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    RecipeCategoryDto Category,
    RecipeAuthorDto Author,
    RecipeNutritionDto? Nutrition,
    IReadOnlyList<RecipeIngredientDto> Ingredients,
    IReadOnlyList<RecipeStepDto> Steps,
    IReadOnlyList<RecipeImageDto> Images
);

public sealed record RecipeCategoryDto(Guid Id, string Name, string Slug);
public sealed record RecipeAuthorDto(string Id, string DisplayName, string? AvatarUrl);
public sealed record RecipeNutritionDto(
    decimal? Calories, 
    decimal? Protein, 
    decimal? Carbohydrates, 
    decimal? Fat, 
    decimal? Fiber, 
    decimal? Sodium
);

public sealed record RecipeIngredientDto(
    Guid Id, 
    string Name, 
    decimal? Quantity, 
    string? Unit, 
    string? Notes, 
    int OrderIndex
);

public sealed record RecipeStepDto(
    Guid Id, 
    int StepNumber, 
    string Title, 
    string Description, 
    int? TimerMinutes, 
    string? ImageUrl
);

public sealed record RecipeImageDto(
    Guid Id, 
    string OriginalUrl, 
    string? AltText, 
    bool IsPrimary, 
    int OrderIndex
);