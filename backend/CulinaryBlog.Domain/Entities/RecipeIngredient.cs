using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

/// <summary>SRS 7.4 + D7 (quantity/unit nullable — hỗ trợ "muối vừa đủ", field "orderIndex").</summary>
public sealed class RecipeIngredient : BaseEntity
{
    public Guid RecipeId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal? Quantity { get; private set; }
    public string? Unit { get; private set; }
    public string? Notes { get; private set; }
    public int OrderIndex { get; private set; }

    private RecipeIngredient()
    {
    }

    public static RecipeIngredient Create(
        Guid recipeId, string name, decimal? quantity = null, string? unit = null,
        string? notes = null, int orderIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("RECIPE_INGREDIENT_NAME_REQUIRED", "Tên nguyên liệu không được để trống.");
        }

        if (quantity is <= 0)
        {
            throw new DomainException("RECIPE_INGREDIENT_QUANTITY_INVALID", "Số lượng nguyên liệu phải > 0 nếu có.");
        }

        return new RecipeIngredient
        {
            RecipeId = recipeId,
            Name = name,
            Quantity = quantity,
            Unit = unit,
            Notes = notes,
            OrderIndex = orderIndex,
        };
    }
}
