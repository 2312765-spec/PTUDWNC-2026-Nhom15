using CulinaryBlog.Application.Recipes.Queries.SearchRecipes;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

public sealed class SearchRecipesTests
{
    private readonly SearchRecipesQueryValidator _validator = new();

    [Theory(DisplayName = "FR-SRCH-001/D4: Từ khóa rỗng hoặc < 2 ký tự → 400 VALIDATION_ERROR")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    public void Validator_InvalidQuery_Fails(string invalidQuery)
    {
        var query = new SearchRecipesQuery(invalidQuery);
        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SearchRecipesQuery.Q));
    }

    [Fact(DisplayName = "FR-SRCH-001: Từ khóa hợp lệ (≥ 2 ký tự) → Hợp lệ")]
    public void Validator_ValidQuery_Passes()
    {
        var query = new SearchRecipesQuery("pho bo", 1, 10);
        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }
}