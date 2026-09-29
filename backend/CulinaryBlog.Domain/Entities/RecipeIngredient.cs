using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

public class RecipeIngredient : BaseEntity
{
    public Guid RecipeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string? Preparation { get; set; }
    public Recipe? Recipe { get; set; }

    public void Update(string name, string amount, string? unit, string? preparation)
    {
        Name = name;
        Amount = amount;
        Unit = unit;
        Preparation = preparation;
        UpdatedAt = DateTime.UtcNow;
    }
}