using CulinaryBlog.Application.Recipes.Commands;
using CulinaryBlog.Application.Recipes.Commands.Create;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-RCP-003 — ràng buộc dữ liệu đầu vào (D4 → 400, D6, D7, D19, bảng "Ràng buộc validator").</summary>
public class CreateRecipeCommandValidatorTests
{
    private static readonly Guid ExistingCategoryId = Guid.NewGuid();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();

    public CreateRecipeCommandValidatorTests()
    {
        _categories.GetByIdAsync(ExistingCategoryId, Arg.Any<CancellationToken>())
            .Returns(Category.Create("Món cuốn", "mon-cuon"));
    }

    private CreateRecipeCommandValidator Validator() => new(_categories);

    private static CreateRecipeCommand Valid() => new(
        Title: "Gỏi cuốn tôm thịt",
        Description: "Món cuốn tươi mát",
        CategoryId: ExistingCategoryId,
        PrepTime: 20,
        CookTime: 10,
        Servings: 4,
        Difficulty: "Easy",
        Instructions: null,
        Nutrition: new RecipeNutritionInput(250, 12, 30, 8, null, null),
        Steps: [new RecipeStepInput("Sơ chế", "Luộc tôm và thịt", 10, null)],
        Ingredients: [new RecipeIngredientInput("Muối", null, null, "vừa đủ")]);

    [Fact(DisplayName = "FR-RCP-003: request hợp lệ → không lỗi")]
    public async Task Valid_Passes()
    {
        (await Validator().ValidateAsync(Valid())).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "FR-RCP-003/D19: cookTime = 0 hợp lệ")]
    public async Task ZeroCookTime_Passes()
    {
        (await Validator().ValidateAsync(Valid() with { CookTime = 0 })).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "FR-RCP-003/D14: difficulty nhận tên hoặc số 1–4, không phân biệt hoa thường")]
    [InlineData("hard")]
    [InlineData("EXPERT")]
    [InlineData("3")]
    public async Task Difficulty_NameOrNumber_Passes(string difficulty)
    {
        (await Validator().ValidateAsync(Valid() with { Difficulty = difficulty })).IsValid.Should().BeTrue();
    }

    public static TheoryData<string, CreateRecipeCommand> InvalidCases => new()
    {
        { "Title", Valid() with { Title = "Gỏi" } },
        { "Title", Valid() with { Title = "   Gỏi    " } },
        { "Title", Valid() with { Title = new string('a', 201) } },
        { "Description", Valid() with { Description = "" } },
        { "Description", Valid() with { Description = new string('a', 2001) } },
        { "PrepTime", Valid() with { PrepTime = 0 } },
        { "CookTime", Valid() with { CookTime = -1 } },
        { "Servings", Valid() with { Servings = 0 } },
        { "Difficulty", Valid() with { Difficulty = "Super" } },
        { "Difficulty", Valid() with { Difficulty = "" } },
        { "CategoryId", Valid() with { CategoryId = Guid.Empty } },
        { "CategoryId", Valid() with { CategoryId = Guid.NewGuid() } },
        { "Nutrition.Calories", Valid() with { Nutrition = new RecipeNutritionInput(-1, null, null, null, null, null) } },
        { "Steps[0].Title", Valid() with { Steps = [new RecipeStepInput("", "Mô tả", null, null)] } },
        { "Steps[0].Description", Valid() with { Steps = [new RecipeStepInput("Bước", new string('a', 2001), null, null)] } },
        { "Steps[0].TimerMinutes", Valid() with { Steps = [new RecipeStepInput("Bước", "Mô tả", -5, null)] } },
        { "Steps[0].ImageUrl", Valid() with { Steps = [new RecipeStepInput("Bước", "Mô tả", null, "javascript:alert(1)")] } },
        { "Ingredients[0].Name", Valid() with { Ingredients = [new RecipeIngredientInput("", 1, "g", null)] } },
        { "Ingredients[0].Quantity", Valid() with { Ingredients = [new RecipeIngredientInput("Muối", 0, "g", null)] } },
        { "Ingredients[0].Unit", Valid() with { Ingredients = [new RecipeIngredientInput("Muối", 1, new string('g', 51), null)] } },
    };

    [Theory(DisplayName = "FR-RCP-003/D4: dữ liệu sai → lỗi đúng trường (→ 400 VALIDATION_ERROR)")]
    [MemberData(nameof(InvalidCases))]
    public async Task Invalid_FailsOnProperty(string property, CreateRecipeCommand command)
    {
        var result = await Validator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == property);
    }

    [Fact(DisplayName = "FR-RCP-003/D7: nguyên liệu không có quantity/unit (\"muối vừa đủ\") hợp lệ")]
    public async Task Ingredient_NullQuantityAndUnit_Passes()
    {
        var command = Valid() with { Ingredients = [new RecipeIngredientInput("Tiêu", null, null, null)] };

        (await Validator().ValidateAsync(command)).IsValid.Should().BeTrue();
    }
}
