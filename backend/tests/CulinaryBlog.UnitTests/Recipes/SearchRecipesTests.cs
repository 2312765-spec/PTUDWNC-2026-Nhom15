using CulinaryBlog.Application.Recipes.Queries.SearchRecipes;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;
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

    [Fact(DisplayName = "FR-SRCH-001/D49: giữ thứ tự ts_rank của repository và trả RelevanceScore")]
    public async Task Handler_KeepsRankOrderAndMapsRelevanceScore()
    {
        var repository = Substitute.For<IRecipeRepository>();
        var best = Recipe.Create(title: "Phở bò", slug: "pho-bo", status: RecipeStatus.Published);
        var other = Recipe.Create(title: "Bún bò", slug: "bun-bo", status: RecipeStatus.Published);
        IReadOnlyList<(Recipe Recipe, float Rank)> hits = [(best, 0.9f), (other, 0.2f)];
        repository.SearchPublishedRecipesAsync("pho", 1, 10, Arg.Any<CancellationToken>()).Returns((hits, 2));

        var result = await new SearchRecipesQueryHandler(repository)
            .Handle(new SearchRecipesQuery("pho", 1, 10), CancellationToken.None);

        result.Items.Select(r => r.Slug).Should().Equal("pho-bo", "bun-bo");
        result.Items.Select(r => r.RelevanceScore).Should().Equal(0.9f, 0.2f);
        result.TotalCount.Should().Be(2);
    }
}
