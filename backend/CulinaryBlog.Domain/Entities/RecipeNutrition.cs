namespace CulinaryBlog.Domain.Entities;

/// <summary>
/// SRS 7.2.1 — Owned Entity, không có bảng riêng. Cột nhúng vào Recipes với tiền tố "Nutrition_".
/// </summary>
public sealed class RecipeNutrition
{
    public decimal? Calories { get; private set; }
    public decimal? Protein { get; private set; }
    public decimal? Carbohydrates { get; private set; }
    public decimal? Fat { get; private set; }
    public decimal? Fiber { get; private set; }
    public decimal? Sodium { get; private set; }

    private RecipeNutrition()
    {
    }

    public static RecipeNutrition Empty() => new();

    public static RecipeNutrition Create(
        decimal? calories, decimal? protein, decimal? carbohydrates,
        decimal? fat, decimal? fiber, decimal? sodium) => new()
        {
            Calories = calories,
            Protein = protein,
            Carbohydrates = carbohydrates,
            Fat = fat,
            Fiber = fiber,
            Sodium = sodium,
        };
}
