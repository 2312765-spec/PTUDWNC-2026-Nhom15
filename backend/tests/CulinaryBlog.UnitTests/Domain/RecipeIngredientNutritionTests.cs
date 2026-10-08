using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Domain;

/// <summary>SRS 7.2.1 (RecipeNutrition) + 7.4 (RecipeIngredient) + D3.</summary>
public class RecipeIngredientNutritionTests
{
    private static Recipe NewRecipe() =>
        Recipe.Create("Bò kho", "bo-kho", "Món bò kho", 20, 90, 4,
            RecipeDifficulty.Medium, Guid.NewGuid(), "author-1");

    [Fact(DisplayName = "SRS 7.4: nguyên liệu mới xếp cuối — OrderIndex tăng dần từ 0")]
    public void AddIngredient_AssignsIncreasingOrderIndex()
    {
        var recipe = NewRecipe();

        var first = recipe.AddIngredient("Thịt bò", 1m, "kg");
        var second = recipe.AddIngredient("Sả", 3m, "cây", "đập dập");

        first.OrderIndex.Should().Be(0);
        second.OrderIndex.Should().Be(1);
        second.Notes.Should().Be("đập dập");
        second.RecipeId.Should().Be(recipe.Id);
    }

    [Fact(DisplayName = "SRS 7.4: Quantity null cho nguyên liệu \"vừa đủ\"")]
    public void AddIngredient_NullQuantity_IsAllowed()
    {
        var ingredient = NewRecipe().AddIngredient("Muối", null, null, "vừa đủ");

        ingredient.Quantity.Should().BeNull();
    }

    [Fact(DisplayName = "SRS 7.2.1: recipe mới có Nutrition rỗng (không null), mọi giá trị null")]
    public void NewRecipe_HasEmptyNutrition()
    {
        var recipe = NewRecipe();

        recipe.Nutrition.Should().NotBeNull();
        recipe.Nutrition.Calories.Should().BeNull();
        recipe.Nutrition.Sodium.Should().BeNull();
    }

    [Fact(DisplayName = "SRS 7.2.1: SetNutrition ghi đủ 6 chỉ số dạng decimal")]
    public void SetNutrition_SetsAllSixValues()
    {
        var recipe = NewRecipe();

        recipe.SetNutrition(520.5m, 35m, 40.25m, 18m, fiber: 4m, sodium: 980m);

        recipe.Nutrition.Calories.Should().Be(520.5m);
        recipe.Nutrition.Protein.Should().Be(35m);
        recipe.Nutrition.Carbohydrates.Should().Be(40.25m);
        recipe.Nutrition.Fat.Should().Be(18m);
        recipe.Nutrition.Fiber.Should().Be(4m);
        recipe.Nutrition.Sodium.Should().Be(980m);
    }

    [Fact(DisplayName = "FR-RCP-005/D3: có ≥ 1 step VÀ ≥ 1 ingredient thì publish được")]
    public void Publish_WithStepAndIngredient_Succeeds()
    {
        var recipe = NewRecipe();
        recipe.AddStep(1, "Ướp thịt", "Ướp với sả và ngũ vị hương.");
        recipe.AddIngredient("Thịt bò", 1m, "kg");

        recipe.Publish();

        recipe.Status.Should().Be(RecipeStatus.Published);
    }
}
