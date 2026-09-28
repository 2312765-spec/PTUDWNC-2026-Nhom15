using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

public class RecipeNutrition : BaseEntity
{
    public Guid RecipeId { get; set; }
    public int Calories { get; set; }
    public int Protein { get; set; }
    public int Carbs { get; set; }
    public int Fat { get; set; }
    public Recipe? Recipe { get; set; }

    public void Update(int calories, int protein, int carbs, int fat)
    {
        Calories = calories;
        Protein = protein;
        Carbs = carbs;
        Fat = fat;
        UpdatedAt = DateTime.UtcNow;
    }
}