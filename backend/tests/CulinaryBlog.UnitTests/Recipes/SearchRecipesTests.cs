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

    [Fact(DisplayName = "FR-SRCH-004: pageSize > 50 không bị validator chặn (handler clamp)")]
    public void Validator_PageSizeOverMax_Passes()
    {
        var result = _validator.Validate(new SearchRecipesQuery("pho", 1, 100));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "FR-SRCH-001/D8: cache key dạng recipes:search:{hash}, không chứa từ khóa thô")]
    public void CacheKey_IsHashed()
    {
        var key = new SearchRecipesQuery("Phở Bò").CacheKey;

        key.Should().MatchRegex("^recipes:search:[0-9a-f]{64}$");
        key.Should().Be(new SearchRecipesQuery("  phở bò ").CacheKey);
    }
}