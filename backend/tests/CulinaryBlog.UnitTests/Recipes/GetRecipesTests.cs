using CulinaryBlog.Application.Recipes.Queries.GetRecipes;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

public sealed class GetRecipesTests
{
    private readonly GetRecipesQueryValidator _validator = new();
    private readonly IRecipeRepository _repository = Substitute.For<IRecipeRepository>();

    public GetRecipesTests()
    {
        _repository.GetPagedRecipesAsync(
                default, default, default, default, default, default, default, default, default, default)
            .ReturnsForAnyArgs(((IReadOnlyList<Recipe>)[], 0));
    }

    [Theory(DisplayName = "FR-SRCH-002/D14: difficulty Easy|Medium|Hard|Expert (hoặc 1–4) hợp lệ")]
    [InlineData("Easy")]
    [InlineData("medium")]
    [InlineData("HARD")]
    [InlineData("Expert")]
    [InlineData("4")]
    public void Validator_ValidDifficulty_Passes(string difficulty)
    {
        _validator.Validate(new GetRecipesQuery(Difficulty: difficulty)).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "FR-SRCH-002/D4: difficulty lạ → lỗi validation (400)")]
    public void Validator_UnknownDifficulty_Fails()
    {
        _validator.Validate(new GetRecipesQuery(Difficulty: "Legendary")).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "FR-SRCH-003/D4: sort ngoài danh sách → lỗi validation (400)")]
    public void Validator_UnknownSort_Fails()
    {
        _validator.Validate(new GetRecipesQuery(Sort: "servings")).IsValid.Should().BeFalse();
    }

    [Theory(DisplayName = "FR-SRCH-004/D4: page < 1 hoặc pageSize < 1 → lỗi validation (400)")]
    [InlineData(0, 12)]
    [InlineData(1, 0)]
    public void Validator_InvalidPaging_Fails(int page, int pageSize)
    {
        _validator.Validate(new GetRecipesQuery(Page: page, PageSize: pageSize)).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "FR-SRCH-004: pageSize > 50 không bị validator chặn (handler clamp)")]
    public void Validator_PageSizeOverMax_Passes()
    {
        _validator.Validate(new GetRecipesQuery(PageSize: 100)).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "FR-RCP-001/D8: cache key dạng recipes:list:{hash}, tách theo phạm vi người xem")]
    public void CacheKey_IsHashed_AndScopedByViewer()
    {
        var guest = new GetRecipesQuery().CacheKey;
        var author = new GetRecipesQuery(CurrentUserId: "author-1").CacheKey;
        var otherAuthor = new GetRecipesQuery(CurrentUserId: "author-2").CacheKey;
        var admin = new GetRecipesQuery(CurrentUserId: "author-1", IsAdmin: true).CacheKey;

        guest.Should().MatchRegex("^recipes:list:[0-9a-f]{64}$");
        new[] { guest, author, otherAuthor, admin }.Should().OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "FR-RCP-001: Guest → chỉ Published (không owner, không all)")]
    public async Task Handle_Guest_OnlyPublished()
    {
        await Handle(new GetRecipesQuery());

        await _repository.Received(1).GetPagedRecipesAsync(
            null, null, null, null, Arg.Any<string?>(), false, Arg.Is<string?>(x => x == null), 1, 12, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-RCP-001: Author → Published + Draft/Archived của chính mình")]
    public async Task Handle_Author_IncludesOwnNonPublished()
    {
        await Handle(new GetRecipesQuery(CurrentUserId: "author-1"));

        await _repository.Received(1).GetPagedRecipesAsync(
            null, null, null, null, Arg.Any<string?>(), false, "author-1", 1, 12, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-RCP-001: Admin → mọi trạng thái")]
    public async Task Handle_Admin_IncludesAllStatuses()
    {
        await Handle(new GetRecipesQuery(CurrentUserId: "admin-1", IsAdmin: true));

        await _repository.Received(1).GetPagedRecipesAsync(
            null, null, null, null, Arg.Any<string?>(), true, Arg.Is<string?>(x => x == null), 1, 12, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-SRCH-002/004: difficulty chuyển sang enum, pageSize > 50 clamp về 50")]
    public async Task Handle_ParsesDifficulty_AndClampsPageSize()
    {
        var result = await Handle(new GetRecipesQuery(Difficulty: "expert", PageSize: 100));

        result.PageSize.Should().Be(50);
        await _repository.Received(1).GetPagedRecipesAsync(
            null, RecipeDifficulty.Expert, null, null, Arg.Any<string?>(), false, Arg.Is<string?>(x => x == null), 1, 50, Arg.Any<CancellationToken>());
    }

    private Task<Application.Common.Models.PagedResult<Application.Categories.DTOs.RecipeSummaryDto>> Handle(GetRecipesQuery query) =>
        new GetRecipesQueryHandler(_repository).Handle(query, CancellationToken.None);
}
