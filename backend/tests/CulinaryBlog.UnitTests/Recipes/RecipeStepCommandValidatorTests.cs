using CulinaryBlog.Application.Recipes.Commands.Steps;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-RCP-010 — D6 (title ≤ 200, description 1–2000, timerMinutes ≥ 0, imageUrl ≤ 500) + D73.</summary>
public class RecipeStepCommandValidatorTests
{
    private static readonly Guid RecipeId = Guid.NewGuid();

    [Fact(DisplayName = "FR-RCP-010: thêm bước hợp lệ → không lỗi")]
    public void Add_Valid_Passes()
    {
        new AddRecipeStepCommandValidator()
            .Validate(new AddRecipeStepCommand(RecipeId, "Sơ chế", "Rửa rau", 0, "https://cdn.example.com/a.jpg"))
            .IsValid.Should().BeTrue();
    }

    public static TheoryData<string, string, string, int?, string?> InvalidAddCases => new()
    {
        { "Title", "", "Mô tả", null, null },
        { "Title", new string('a', 201), "Mô tả", null, null },
        { "Description", "Bước", "", null, null },
        { "Description", "Bước", new string('a', 2001), null, null },
        { "TimerMinutes", "Bước", "Mô tả", -1, null },
        { "ImageUrl", "Bước", "Mô tả", null, "javascript:alert(1)" },
        { "ImageUrl", "Bước", "Mô tả", null, "https://cdn.example.com/" + new string('a', 500) },
    };

    [Theory(DisplayName = "FR-RCP-010/D6: dữ liệu bước sai → lỗi đúng trường (400 VALIDATION_ERROR)")]
    [MemberData(nameof(InvalidAddCases))]
    public void Add_Invalid_FailsOnProperty(string property, string title, string description, int? timer, string? imageUrl)
    {
        var result = new AddRecipeStepCommandValidator()
            .Validate(new AddRecipeStepCommand(RecipeId, title, description, timer, imageUrl));

        result.Errors.Should().Contain(e => e.PropertyName == property);
    }

    [Theory(DisplayName = "FR-RCP-010/D6: PUT — stepNumber tùy chọn, nếu có phải ≥ 1")]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-2, false)]
    public void Update_StepNumber(int? stepNumber, bool valid)
    {
        var result = new UpdateRecipeStepCommandValidator()
            .Validate(new UpdateRecipeStepCommand(RecipeId, Guid.NewGuid(), "Bước", "Mô tả", null, null, stepNumber));

        result.IsValid.Should().Be(valid);
    }
}
