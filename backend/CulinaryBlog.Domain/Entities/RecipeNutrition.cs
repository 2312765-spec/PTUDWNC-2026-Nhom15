namespace CulinaryBlog.Domain.Entities;

/// <summary>
/// SRS 7.2.1 — Owned Entity của Recipe: không có bảng riêng, các cột nằm trong bảng Recipes với
/// tiền tố "Nutrition_". Mọi giá trị tính trên 1 khẩu phần, đều nullable (công thức có thể không
/// khai báo dinh dưỡng). Chỉ đổi qua <see cref="Recipe.SetNutrition"/>.
/// </summary>
public sealed class RecipeNutrition
{
    /// <summary>kcal / serving.</summary>
    public decimal? Calories { get; private set; }

    /// <summary>gram / serving.</summary>
    public decimal? Protein { get; private set; }

    /// <summary>gram / serving.</summary>
    public decimal? Carbohydrates { get; private set; }

    /// <summary>gram / serving.</summary>
    public decimal? Fat { get; private set; }

    /// <summary>gram / serving.</summary>
    public decimal? Fiber { get; private set; }

    /// <summary>mg / serving.</summary>
    public decimal? Sodium { get; private set; }

    internal RecipeNutrition()
    {
    }

    internal RecipeNutrition(decimal? calories, decimal? protein, decimal? carbohydrates, decimal? fat, decimal? fiber, decimal? sodium)
    {
        Calories = calories;
        Protein = protein;
        Carbohydrates = carbohydrates;
        Fat = fat;
        Fiber = fiber;
        Sodium = sodium;
    }
}
