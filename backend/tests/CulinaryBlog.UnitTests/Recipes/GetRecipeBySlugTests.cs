using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Application.Recipes.Queries.GetRecipeBySlug;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using MediatR;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-RCP-002 — phân quyền xem chi tiết (GetRecipeBySlugQuery) và tải dữ liệu có cache (GetRecipeDetailQuery).</summary>
public sealed class GetRecipeBySlugTests
{
    private const string Slug = "pho-bo";
    private const string AuthorId = "author-1";

    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IRecipeRepository _repository = Substitute.For<IRecipeRepository>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();

    [Fact(DisplayName = "FR-RCP-002: Published → khách xem được")]
    public async Task Published_Guest_ReturnsRecipe()
    {
        var dto = GivenCachedDetail(RecipeStatus.Published);
        GivenGuest();

        var result = await BySlugHandler().Handle(new GetRecipeBySlugQuery(Slug), CancellationToken.None);

        result.Should().BeSameAs(dto);
    }

    [Theory(DisplayName = "FR-RCP-002/A2: Draft/Archived → khách bị 403 RECIPE_FORBIDDEN")]
    [InlineData(RecipeStatus.Draft)]
    [InlineData(RecipeStatus.Archived)]
    public async Task NotPublished_Guest_Throws403(RecipeStatus status)
    {
        GivenCachedDetail(status);
        GivenGuest();

        var act = () => BySlugHandler().Handle(new GetRecipeBySlugQuery(Slug), CancellationToken.None);

        (await act.Should().ThrowAsync<ForbiddenException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeForbidden);
    }

    [Fact(DisplayName = "FR-RCP-002/A2,NFR-SEC-006: Draft → user khác (không phải Admin) bị 403")]
    public async Task Draft_OtherUser_Throws403()
    {
        GivenCachedDetail(RecipeStatus.Draft);
        GivenUser("someone-else", isAdmin: false);

        var act = () => BySlugHandler().Handle(new GetRecipeBySlugQuery(Slug), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact(DisplayName = "FR-RCP-002: Draft → tác giả xem được")]
    public async Task Draft_Owner_ReturnsRecipe()
    {
        var dto = GivenCachedDetail(RecipeStatus.Draft);
        GivenUser(AuthorId, isAdmin: false);

        var result = await BySlugHandler().Handle(new GetRecipeBySlugQuery(Slug), CancellationToken.None);

        result.Should().BeSameAs(dto);
    }

    [Fact(DisplayName = "FR-RCP-002: Draft → Admin xem được")]
    public async Task Draft_Admin_ReturnsRecipe()
    {
        var dto = GivenCachedDetail(RecipeStatus.Draft);
        GivenUser("admin-1", isAdmin: true);

        var result = await BySlugHandler().Handle(new GetRecipeBySlugQuery(Slug), CancellationToken.None);

        result.Should().BeSameAs(dto);
    }

    [Fact(DisplayName = "FR-RCP-002/D8: query dữ liệu cache key recipe:{slug}, TTL 5 phút, tag recipes + recipe:{slug}")]
    public void DetailQuery_CacheSettings_MatchD8()
    {
        var query = new GetRecipeDetailQuery(Slug);

        query.CacheKey.Should().Be("recipe:pho-bo");
        query.CacheTtl.Should().Be(TimeSpan.FromMinutes(5));
        query.CacheTags.Should().BeEquivalentTo(["recipes", "recipe:pho-bo"]);
    }

    [Fact(DisplayName = "FR-RCP-002/NFR-SEC-006: query công khai KHÔNG cache — kiểm quyền chạy mọi request")]
    public void BySlugQuery_IsNotCacheable()
    {
        new GetRecipeBySlugQuery(Slug).Should().NotBeAssignableTo<ICacheable>();
    }

    [Fact(DisplayName = "FR-RCP-002/D1: slug không tồn tại hoặc đã xóa → 404 RECIPE_NOT_FOUND")]
    public async Task Detail_NotFound_Throws404()
    {
        _repository.GetBySlugDetailedAsync(Slug, Arg.Any<CancellationToken>()).Returns((Recipe?)null);

        var act = () => DetailHandler().Handle(new GetRecipeDetailQuery(Slug), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeNotFound);
    }

    [Fact(DisplayName = "FR-RCP-002/D5: author lấy displayName + avatarUrl thật của tác giả")]
    public async Task Detail_MapsAuthorProfile()
    {
        GivenRecipeInRepository();
        _identityService.GetUserByIdAsync(AuthorId, Arg.Any<CancellationToken>())
            .Returns(new AuthenticatedUser(AuthorId, "a@test.local", "Bếp Trưởng Yến", "https://cdn/a.png", null, ["Author"]));

        var result = await DetailHandler().Handle(new GetRecipeDetailQuery(Slug), CancellationToken.None);

        result.Author.Should().Be(new RecipeAuthorDto(AuthorId, "Bếp Trưởng Yến", "https://cdn/a.png"));
    }

    [Fact(DisplayName = "FR-RCP-002: tác giả đã bị xóa → vẫn trả công thức, tên hiển thị thay thế")]
    public async Task Detail_AuthorDeleted_UsesFallbackName()
    {
        GivenRecipeInRepository();
        _identityService.GetUserByIdAsync(AuthorId, Arg.Any<CancellationToken>()).Returns((AuthenticatedUser?)null);

        var result = await DetailHandler().Handle(new GetRecipeDetailQuery(Slug), CancellationToken.None);

        result.Author.DisplayName.Should().Be(GetRecipeDetailQueryHandler.DeletedAuthorDisplayName);
        result.Author.AvatarUrl.Should().BeNull();
    }

    [Fact(DisplayName = "FR-RCP-002: ingredients/steps/images sắp theo thứ tự hiển thị")]
    public async Task Detail_OrdersChildCollections()
    {
        var recipe = GivenRecipeInRepository();
        recipe.AddIngredient("Hành", 1, "củ");
        recipe.AddStep(2, "Bước hai", "Mô tả 2");
        recipe.AddStep(1, "Bước một", "Mô tả 1");

        var result = await DetailHandler().Handle(new GetRecipeDetailQuery(Slug), CancellationToken.None);

        result.Steps.Select(s => s.StepNumber).Should().ContainInOrder(1, 2);
        result.Ingredients.Should().ContainSingle(i => i.Name == "Hành");
    }

    private GetRecipeBySlugQueryHandler BySlugHandler() => new(_sender, _currentUser);

    private GetRecipeDetailQueryHandler DetailHandler() => new(_repository, _identityService);

    private RecipeDetailDto GivenCachedDetail(RecipeStatus status)
    {
        var dto = new RecipeDetailDto(
            Guid.NewGuid(), "Phở bò", Slug, "Mô tả", null, (short)RecipeDifficulty.Easy, (short)status,
            15, 30, 4, null, DateTime.UtcNow, null,
            new RecipeCategoryDto(Guid.NewGuid(), "Món Việt", "mon-viet"),
            new RecipeAuthorDto(AuthorId, "Tác giả", null),
            null, [], [], []);
        _sender.Send(Arg.Is<GetRecipeDetailQuery>(q => q.Slug == Slug), Arg.Any<CancellationToken>()).Returns(dto);
        return dto;
    }

    private Recipe GivenRecipeInRepository()
    {
        var recipe = Recipe.Create(slug: Slug, authorId: AuthorId, categoryId: Guid.NewGuid(), status: RecipeStatus.Published);
        _repository.GetBySlugDetailedAsync(Slug, Arg.Any<CancellationToken>()).Returns(recipe);
        return recipe;
    }

    private void GivenGuest()
    {
        _currentUser.IsAuthenticated.Returns(false);
        _currentUser.UserId.Returns((string?)null);
        _currentUser.IsAdmin.Returns(false);
    }

    private void GivenUser(string userId, bool isAdmin)
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);
        _currentUser.IsAdmin.Returns(isAdmin);
    }
}
