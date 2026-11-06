using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Domain;

/// <summary>Quy tắc nghiệp vụ của aggregate Recipe — FR-RCP-003 (tạo) + D6, D18, D19.</summary>
public class RecipeTests
{
    private static Recipe NewRecipe(int prepTime = 15, int cookTime = 30, int servings = 4, string? instructions = null) =>
        Recipe.Create("Gỏi cuốn tôm thịt", "goi-cuon", "Món cuốn", prepTime, cookTime, servings,
            RecipeDifficulty.Easy, Guid.NewGuid(), "author-1", instructions: instructions);

    [Fact(DisplayName = "FR-RCP-003: recipe mới luôn ở trạng thái Draft, chưa có PublishedAt")]
    public void Create_StartsAsDraft()
    {
        var recipe = NewRecipe();

        recipe.Status.Should().Be(RecipeStatus.Draft);
        recipe.PublishedAt.Should().BeNull();
        recipe.Slug.Should().Be("goi-cuon");
        recipe.AuthorId.Should().Be("author-1");
    }

    [Fact(DisplayName = "FR-RCP-003/D18: Instructions là tùy chọn — lưu khi có, null khi không gửi")]
    public void Create_StoresOptionalInstructions()
    {
        NewRecipe(instructions: "Cuốn chặt tay").Instructions.Should().Be("Cuốn chặt tay");
        NewRecipe().Instructions.Should().BeNull();
    }

    [Fact(DisplayName = "FR-RCP-003/D19: cookTime = 0 hợp lệ (món không cần nấu)")]
    public void Create_ZeroCookTime_IsAllowed()
    {
        NewRecipe(cookTime: 0).CookTime.Should().Be(0);
    }

    [Theory(DisplayName = "FR-RCP-003/D19: prepTime ≤ 0, cookTime < 0, servings ≤ 0 → DomainException")]
    [InlineData(0, 10, 2)]
    [InlineData(10, -1, 2)]
    [InlineData(10, 10, 0)]
    public void Create_InvalidNumbers_Throws(int prepTime, int cookTime, int servings)
    {
        var act = () => NewRecipe(prepTime, cookTime, servings);

        act.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("VALIDATION_ERROR");
    }

    [Fact(DisplayName = "FR-RCP-010/D6: AppendStep đánh số Max + 1, bắt đầu từ 1")]
    public void AppendStep_NumbersSequentially()
    {
        var recipe = NewRecipe();

        var first = recipe.AppendStep("Sơ chế", "Luộc tôm", null, null);
        var second = recipe.AppendStep("Cuốn", "Cuốn bánh tráng", 5, "https://cdn/x.jpg");

        first.StepNumber.Should().Be(1);
        second.StepNumber.Should().Be(2);
        second.TimerMinutes.Should().Be(5);
        second.ImageUrl.Should().Be("https://cdn/x.jpg");
        second.RecipeId.Should().Be(recipe.Id);
    }

    [Fact(DisplayName = "FR-RCP-010/D6: AppendStep sau dữ liệu có khoảng trống vẫn lấy Max + 1")]
    public void AppendStep_AfterGap_UsesMaxPlusOne()
    {
        var recipe = NewRecipe();
        recipe.AddStep(3, "Cũ", "Bước số 3 từ seed");

        recipe.AppendStep("Mới", "Bước mới", null, null).StepNumber.Should().Be(4);
    }
}
