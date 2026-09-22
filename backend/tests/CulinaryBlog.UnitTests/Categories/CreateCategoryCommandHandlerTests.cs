using CulinaryBlog.Application.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Categories;

/// <summary>FR-CAT-003 — luồng chính + D10 (auto-suffix slug) qua mock, không cần DB.</summary>
public class CreateCategoryCommandHandlerTests
{
    private readonly ICategoryRepository _categoryRepository = Substitute.For<ICategoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ISlugHelper _slugHelper = Substitute.For<ISlugHelper>();

    private CreateCategoryCommandHandler CreateHandler() => new(_categoryRepository, _unitOfWork, _slugHelper);

    [Fact(DisplayName = "FR-CAT-003: tạo thành công → gọi AddAsync + SaveChanges, trả CategoryDto")]
    public async Task Handle_Success_AddsCategoryAndSaves()
    {
        _categoryRepository.ExistsByNameAsync("Món chay", Arg.Any<CancellationToken>()).Returns(false);
        _slugHelper.Generate("Món chay").Returns("mon-chay");
        _categoryRepository.ExistsBySlugAsync("mon-chay", Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(new CreateCategoryCommand("Món chay", "Không thịt cá"), CancellationToken.None);

        result.Name.Should().Be("Món chay");
        result.Slug.Should().Be("mon-chay");
        result.RecipeCount.Should().Be(0);
        await _categoryRepository.Received(1).AddAsync(Arg.Is<Category>(c => c.Slug == "mon-chay"), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-CAT-003/D4: tên trùng → ConflictException CATEGORY_NAME_EXISTS (409)")]
    public async Task Handle_DuplicateName_ThrowsConflict()
    {
        _categoryRepository.ExistsByNameAsync("Món chay", Arg.Any<CancellationToken>()).Returns(true);

        var act = () => CreateHandler().Handle(new CreateCategoryCommand("Món chay", null), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.CategoryNameExists);
        await _categoryRepository.DidNotReceive().AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-CAT-003/D10: slug trùng → tự thêm hậu tố -2, không bao giờ ném lỗi")]
    public async Task Handle_DuplicateSlug_AppendsSuffix()
    {
        _categoryRepository.ExistsByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _slugHelper.Generate(Arg.Any<string>()).Returns("mon-chinh");
        _categoryRepository.ExistsBySlugAsync("mon-chinh", Arg.Any<CancellationToken>()).Returns(true);
        _categoryRepository.ExistsBySlugAsync("mon-chinh-2", Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(new CreateCategoryCommand("Món chính", null), CancellationToken.None);

        result.Slug.Should().Be("mon-chinh-2");
    }

    [Fact(DisplayName = "FR-CAT-003: Name/Description được trim trước khi lưu")]
    public async Task Handle_TrimsNameAndDescription()
    {
        _categoryRepository.ExistsByNameAsync("Món chay", Arg.Any<CancellationToken>()).Returns(false);
        _slugHelper.Generate("Món chay").Returns("mon-chay");
        _categoryRepository.ExistsBySlugAsync("mon-chay", Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(new CreateCategoryCommand("  Món chay  ", "  mô tả  "), CancellationToken.None);

        result.Name.Should().Be("Món chay");
        result.Description.Should().Be("mô tả");
    }
}
