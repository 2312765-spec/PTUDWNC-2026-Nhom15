using CulinaryBlog.Application.Categories.Queries.GetCategoryBySlug;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Categories;

/// <summary>
/// FR-CAT-002 qua mock. Lọc Draft/Published, đếm và phân trang chạy trong database
/// (<see cref="IRecipeRepository.GetPagedByCategoryIdAsync"/>, NFR-PERF-004) — điều kiện SQL kiểm bằng
/// integration test (Categories/GetCategoryBySlugTests.cs, CategoryQueryShapeTests.cs). Ở đây kiểm handler
/// truyền đúng người xem / trang và map đúng DTO.
/// </summary>
public class GetCategoryBySlugQueryHandlerTests
{
    private readonly ICategoryRepository _categoryRepository = Substitute.For<ICategoryRepository>();
    private readonly IRecipeRepository _recipeRepository = Substitute.For<IRecipeRepository>();

    private GetCategoryBySlugQueryHandler CreateHandler() => new(_categoryRepository, _recipeRepository);

    [Fact(DisplayName = "FR-CAT-002: slug không tồn tại → NotFoundException CATEGORY_NOT_FOUND")]
    public async Task Handle_SlugNotFound_ThrowsNotFound()
    {
        _categoryRepository.GetBySlugAsync("khong-ton-tai", Arg.Any<CancellationToken>()).Returns((Category?)null);

        var act = () => CreateHandler().Handle(new GetCategoryBySlugQuery("khong-ton-tai"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.CategoryNotFound);
    }

    [Fact(DisplayName = "FR-CAT-002: category tồn tại, chưa có recipe nào → 200 kèm mảng rỗng, không lỗi")]
    public async Task Handle_CategoryWithNoRecipes_ReturnsEmptyList()
    {
        var category = GivenCategory("mon-chinh");
        GivenPage(category.Id, [], 0);

        var result = await CreateHandler().Handle(new GetCategoryBySlugQuery("mon-chinh"), CancellationToken.None);

        result.Recipes.Items.Should().BeEmpty();
        result.Recipes.TotalCount.Should().Be(0);
        result.Category.Slug.Should().Be("mon-chinh");
    }

    [Theory(DisplayName = "FR-CAT-002/NFR-PERF-004: truyền người xem xuống repository (Guest = null → chỉ Published)")]
    [InlineData(null)]
    [InlineData("author-1")]
    public async Task Handle_PassesViewerAsDraftOwner(string? currentUserId)
    {
        var category = GivenCategory("mon-chinh");
        GivenPage(category.Id, [], 0);

        await CreateHandler().Handle(new GetCategoryBySlugQuery("mon-chinh", CurrentUserId: currentUserId), CancellationToken.None);

        await _recipeRepository.Received(1).GetPagedByCategoryIdAsync(
            category.Id, currentUserId, 1, 12, Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "FR-SRCH-004: page < 1 → 1; pageSize > 50 → 50; pageSize < 1 → mặc định")]
    [InlineData(0, 100, 1, 50)]
    [InlineData(3, 0, 3, 12)]
    public async Task Handle_ClampsPaging(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var category = GivenCategory("mon-chinh");
        GivenPage(category.Id, [], 0);

        var result = await CreateHandler().Handle(new GetCategoryBySlugQuery("mon-chinh", page, pageSize), CancellationToken.None);

        await _recipeRepository.Received(1).GetPagedByCategoryIdAsync(
            category.Id, null, expectedPage, expectedPageSize, Arg.Any<CancellationToken>());
        result.Recipes.Page.Should().Be(expectedPage);
        result.Recipes.PageSize.Should().Be(expectedPageSize);
    }

    [Fact(DisplayName = "FR-CAT-002: map trang recipe + tổng số từ repository; recipeCount của danh mục = tổng số")]
    public async Task Handle_MapsPageAndTotal()
    {
        var category = GivenCategory("mon-chinh");
        var recipe = Recipe.Create("Gỏi cuốn", "goi-cuon", categoryId: category.Id, status: RecipeStatus.Published);
        recipe.AttachImage("https://cdn.test/goi.jpg", isPrimary: true);
        GivenPage(category.Id, [recipe], 7);

        var result = await CreateHandler().Handle(new GetCategoryBySlugQuery("mon-chinh"), CancellationToken.None);

        result.Recipes.TotalCount.Should().Be(7);
        result.Category.RecipeCount.Should().Be(7);
        var item = result.Recipes.Items.Should().ContainSingle().Subject;
        item.Slug.Should().Be("goi-cuon");
        item.FeaturedImageUrl.Should().Be("https://cdn.test/goi.jpg");
    }

    private Category GivenCategory(string slug)
    {
        var category = Category.Create("Món chính", slug, "desc", null, 0);
        _categoryRepository.GetBySlugAsync(slug, Arg.Any<CancellationToken>()).Returns(category);
        return category;
    }

    private void GivenPage(Guid categoryId, IReadOnlyList<Recipe> items, int total) =>
        _recipeRepository
            .GetPagedByCategoryIdAsync(categoryId, Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((items, total));
}
