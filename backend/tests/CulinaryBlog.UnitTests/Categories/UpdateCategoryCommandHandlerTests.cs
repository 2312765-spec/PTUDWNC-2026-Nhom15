using CulinaryBlog.Application.Categories.Commands.UpdateCategory;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Categories;

/// <summary>FR-CAT-004 — luồng chính, slug bất biến (D10), 404, 409 (D4) qua mock, không cần DB.</summary>
public class UpdateCategoryCommandHandlerTests
{
    private readonly ICategoryRepository _categoryRepository = Substitute.For<ICategoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private UpdateCategoryCommandHandler CreateHandler() => new(_categoryRepository, _unitOfWork);

    private static Category ExistingCategory() => Category.Create("Món chính", "mon-chinh", "mô tả cũ", null, 0);

    [Fact(DisplayName = "FR-CAT-004/D10: đổi tên thành công → Slug KHÔNG đổi, lưu DB, trả CategoryDto kèm recipeCount")]
    public async Task Handle_Success_KeepsSlugAndSaves()
    {
        var category = ExistingCategory();
        _categoryRepository.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
        _categoryRepository.ExistsByNameAsync("Món sáng", Arg.Any<CancellationToken>()).Returns(false);
        _categoryRepository.GetPublishedRecipeCountAsync(category.Id, Arg.Any<CancellationToken>()).Returns(3);

        var result = await CreateHandler().Handle(new UpdateCategoryCommand(category.Id, "Món sáng", "mô tả mới"), CancellationToken.None);

        result.Name.Should().Be("Món sáng");
        result.Slug.Should().Be("mon-chinh");
        result.Description.Should().Be("mô tả mới");
        result.RecipeCount.Should().Be(3);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-CAT-004: Name và Description được trim trước khi lưu, như FR-CAT-003")]
    public async Task Handle_TrimsNameAndDescription()
    {
        var category = ExistingCategory();
        _categoryRepository.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);

        var result = await CreateHandler().Handle(new UpdateCategoryCommand(category.Id, "  Món sáng  ", "  mô tả  "), CancellationToken.None);

        result.Name.Should().Be("Món sáng");
        result.Description.Should().Be("mô tả");
    }

    [Fact(DisplayName = "FR-CAT-004: chỉ đổi hoa/thường của tên → không bị coi là trùng với chính nó")]
    public async Task Handle_CaseOnlyRename_DoesNotCheckDuplicate()
    {
        var category = ExistingCategory();
        _categoryRepository.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
        _categoryRepository.ExistsByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().Handle(new UpdateCategoryCommand(category.Id, "MÓN CHÍNH", null), CancellationToken.None);

        result.Name.Should().Be("MÓN CHÍNH");
        await _categoryRepository.DidNotReceive().ExistsByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-CAT-004: ID không tồn tại → NotFoundException CATEGORY_NOT_FOUND (404)")]
    public async Task Handle_NotFound_Throws()
    {
        _categoryRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Category?)null);

        var act = () => CreateHandler().Handle(new UpdateCategoryCommand(Guid.NewGuid(), "Món sáng", null), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.CategoryNotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-CAT-004/D4: đổi sang tên đã có ở danh mục khác → ConflictException CATEGORY_NAME_EXISTS (409)")]
    public async Task Handle_DuplicateName_ThrowsConflict()
    {
        var category = ExistingCategory();
        _categoryRepository.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
        _categoryRepository.ExistsByNameAsync("Món chay", Arg.Any<CancellationToken>()).Returns(true);

        var act = () => CreateHandler().Handle(new UpdateCategoryCommand(category.Id, "Món chay", null), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.CategoryNameExists);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-CAT-004/D8: command invalidate cả tag 'categories' và 'recipes'")]
    public void Command_InvalidatesCategoriesAndRecipesTags()
    {
        var command = new UpdateCategoryCommand(Guid.NewGuid(), "Món sáng", null);

        command.TagsToInvalidate.Should().BeEquivalentTo("categories", "recipes");
    }
}
