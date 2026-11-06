using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Domain;

/// <summary>FR-RCP-009 — quản lý danh sách nguyên liệu trong aggregate Recipe: D3, D7, D76.</summary>
public class RecipeIngredientListTests
{
    private static Recipe NewRecipe(int ingredientCount, RecipeStatus status = RecipeStatus.Draft)
    {
        var recipe = Recipe.Create("Phở bò", "pho-bo", "Phở Hà Nội", 30, 180, 4,
            RecipeDifficulty.Hard, Guid.NewGuid(), "author-1", status);
        for (var i = 1; i <= ingredientCount; i++)
        {
            recipe.AddIngredient($"NL {i}", i, "g");
        }

        return recipe;
    }

    private static string[] Names(Recipe recipe) =>
        [.. recipe.Ingredients.OrderBy(i => i.OrderIndex).Select(i => i.Name)];

    [Fact(DisplayName = "FR-RCP-009/D7: AddIngredient không có vị trí → xếp cuối, OrderIndex bắt đầu từ 0")]
    public void Add_WithoutPosition_AppendsAtEnd()
    {
        var recipe = NewRecipe(2);

        var added = recipe.AddIngredient("Hành", null, null);

        added.OrderIndex.Should().Be(2);
        recipe.Ingredients.Select(i => i.OrderIndex).Should().BeEquivalentTo([0, 1, 2]);
    }

    [Theory(DisplayName = "FR-RCP-009/D76: AddIngredient có vị trí → chèn vào đó, đánh lại 0..n-1")]
    [InlineData(0, new[] { "Mới", "NL 1", "NL 2", "NL 3" })]
    [InlineData(1, new[] { "NL 1", "Mới", "NL 2", "NL 3" })]
    [InlineData(3, new[] { "NL 1", "NL 2", "NL 3", "Mới" })]
    public void Add_WithPosition_Inserts(int position, string[] expected)
    {
        var recipe = NewRecipe(3);

        recipe.AddIngredient("Mới", null, null, position: position);

        Names(recipe).Should().Equal(expected);
        recipe.Ingredients.Select(i => i.OrderIndex).Should().BeEquivalentTo([0, 1, 2, 3]);
    }

    [Theory(DisplayName = "FR-RCP-009/D76: AddIngredient vị trí ngoài 0..số nguyên liệu → DomainException VALIDATION_ERROR")]
    [InlineData(-1)]
    [InlineData(4)]
    public void Add_PositionOutOfRange_Throws(int position)
    {
        var recipe = NewRecipe(3);

        var act = () => recipe.AddIngredient("Mới", null, null, position: position);

        act.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("VALIDATION_ERROR");
        recipe.Ingredients.Should().HaveCount(3);
    }

    [Theory(DisplayName = "FR-RCP-009/D76: UpdateIngredient đổi vị trí → đánh lại 0..n-1")]
    [InlineData(2, 0, new[] { "NL 3", "NL 1", "NL 2" })]
    [InlineData(0, 2, new[] { "NL 2", "NL 3", "NL 1" })]
    [InlineData(1, 1, new[] { "NL 1", "NL 2", "NL 3" })]
    public void Update_Moves(int index, int newPosition, string[] expected)
    {
        var recipe = NewRecipe(3);
        var target = recipe.Ingredients.OrderBy(i => i.OrderIndex).ElementAt(index);

        recipe.UpdateIngredient(target.Id, target.Name, 1, "g", null, newPosition);

        Names(recipe).Should().Equal(expected);
        recipe.Ingredients.Select(i => i.OrderIndex).Should().BeEquivalentTo([0, 1, 2]);
    }

    [Fact(DisplayName = "FR-RCP-009/D7: UpdateIngredient không gửi vị trí → giữ vị trí, null xóa quantity/unit/notes")]
    public void Update_ContentOnly()
    {
        var recipe = NewRecipe(2);
        var target = recipe.Ingredients.First(i => i.Name == "NL 2");

        recipe.UpdateIngredient(target.Id, "Muối", null, null, null, null);

        target.Name.Should().Be("Muối");
        target.Quantity.Should().BeNull();
        target.Unit.Should().BeNull();
        target.OrderIndex.Should().Be(1);
    }

    [Theory(DisplayName = "FR-RCP-009/D76: UpdateIngredient vị trí ngoài 0..n-1 → DomainException VALIDATION_ERROR, không đổi gì")]
    [InlineData(-1)]
    [InlineData(3)]
    public void Update_PositionOutOfRange_Throws(int position)
    {
        var recipe = NewRecipe(3);
        var target = recipe.Ingredients.First();

        var act = () => recipe.UpdateIngredient(target.Id, "Đổi tên", null, null, null, position);

        act.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("VALIDATION_ERROR");
        target.Name.Should().Be("NL 1");
    }

    [Theory(DisplayName = "FR-RCP-009/D76: RemoveIngredient đầu / giữa / cuối → đánh lại 0..n-1")]
    [InlineData(0, new[] { "NL 2", "NL 3" })]
    [InlineData(1, new[] { "NL 1", "NL 3" })]
    [InlineData(2, new[] { "NL 1", "NL 2" })]
    public void Remove_Reindexes(int index, string[] expected)
    {
        var recipe = NewRecipe(3);
        var target = recipe.Ingredients.OrderBy(i => i.OrderIndex).ElementAt(index);

        recipe.RemoveIngredient(target.Id).Should().BeSameAs(target);

        Names(recipe).Should().Equal(expected);
        recipe.Ingredients.Select(i => i.OrderIndex).Should().BeEquivalentTo([0, 1]);
    }

    [Fact(DisplayName = "FR-RCP-009/D3/D76: không xóa được nguyên liệu cuối cùng của recipe Published")]
    public void Remove_LastOfPublished_Throws()
    {
        var recipe = NewRecipe(1, RecipeStatus.Published);

        var act = () => recipe.RemoveIngredient(recipe.Ingredients.Single().Id);

        act.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("RECIPE_PUBLISH_INCOMPLETE");
        recipe.Ingredients.Should().HaveCount(1);
    }

    [Fact(DisplayName = "FR-RCP-009: xóa nguyên liệu cuối cùng của Draft → được")]
    public void Remove_LastOfDraft_Allowed()
    {
        var recipe = NewRecipe(1);

        recipe.RemoveIngredient(recipe.Ingredients.Single().Id);

        recipe.Ingredients.Should().BeEmpty();
    }

    [Fact(DisplayName = "FR-RCP-009/D76: nguyên liệu không thuộc recipe → DomainException RECIPE_INGREDIENT_NOT_FOUND")]
    public void UnknownIngredient_Throws()
    {
        var recipe = NewRecipe(1);

        var update = () => recipe.UpdateIngredient(Guid.NewGuid(), "X", null, null, null, null);
        var remove = () => recipe.RemoveIngredient(Guid.NewGuid());

        update.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("RECIPE_INGREDIENT_NOT_FOUND");
        remove.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("RECIPE_INGREDIENT_NOT_FOUND");
    }
}
