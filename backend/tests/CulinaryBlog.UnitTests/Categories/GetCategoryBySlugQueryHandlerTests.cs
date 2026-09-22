using CulinaryBlog.Application.Categories.Queries.GetCategoryBySlug;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Categories;

/// <summary>
/// FR-CAT-002 qua mock. LƯU Ý: <see cref="Category"/> không có API công khai để gắn
/// <see cref="Recipe"/> vào navigation collection (chỉ EF Core tự nối lúc Include) — nên
/// logic lọc Draft/Published/phân trang KHÔNG kiểm tra được ở đây, phải kiểm bằng
/// integration test thật (Categories/GetCategoryBySlugTests.cs) — đúng chỗ bug thật
/// (repository thiếu .Include) đã xảy ra.
/// </summary>
public class GetCategoryBySlugQueryHandlerTests
{
    private readonly ICategoryRepository _categoryRepository = Substitute.For<ICategoryRepository>();

    private GetCategoryBySlugQueryHandler CreateHandler() => new(_categoryRepository);

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
        var category = Category.Create("Món chính", "mon-chinh", "desc", null, 0);
        _categoryRepository.GetBySlugAsync("mon-chinh", Arg.Any<CancellationToken>()).Returns(category);

        var result = await CreateHandler().Handle(new GetCategoryBySlugQuery("mon-chinh"), CancellationToken.None);

        result.Recipes.Items.Should().BeEmpty();
        result.Recipes.TotalCount.Should().Be(0);
        result.Category.Slug.Should().Be("mon-chinh");
    }
}
