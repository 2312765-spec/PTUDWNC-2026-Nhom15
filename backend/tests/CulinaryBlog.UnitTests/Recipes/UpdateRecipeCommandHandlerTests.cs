using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Application.Recipes.Commands;
using CulinaryBlog.Application.Recipes.Commands.Update;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-RCP-004 — A1 (403), A2 (409, D4/D34/D71), A3 (404), D13 (slug bất biến), D69, D8 invalidation.</summary>
public class UpdateRecipeCommandHandlerTests
{
    private const string OwnerId = "owner-1";
    private static readonly byte[] CurrentRowVersion = [1, 2, 3, 4];
    private readonly IRecipeRepository _recipes = Substitute.For<IRecipeRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly Category _newCategory = Category.Create("Món chay", "mon-chay");
    private readonly Recipe _recipe;

    public UpdateRecipeCommandHandlerTests()
    {
        _recipe = Recipe.Create("Gỏi cuốn tôm thịt", "goi-cuon-tom-thit", "Món cuốn", 20, 10, 4,
            RecipeDifficulty.Easy, Guid.NewGuid(), OwnerId, instructions: "Cuốn chặt tay");
        _recipe.SetNutrition(250, 12, 30, 8);
        _recipe.RowVersion = CurrentRowVersion;

        _recipes.GetByIdWithDetailsAsync(_recipe.Id, Arg.Any<CancellationToken>()).Returns(_recipe);
        _categories.GetByIdAsync(_newCategory.Id, Arg.Any<CancellationToken>()).Returns(_newCategory);
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(OwnerId);
        _currentUser.IsInRole(Roles.Author).Returns(true);
    }

    private UpdateRecipeCommandHandler Handler() =>
        new(_recipes, _categories, _unitOfWork, _identity, new RecipeAuthorizationHandler(_currentUser));

    private UpdateRecipeCommand Command(byte[]? rowVersion = null, RecipeNutritionInput? nutrition = null) => new(
        _recipe.Id,
        $"\"{Convert.ToBase64String(rowVersion ?? CurrentRowVersion)}\"",
        "  Gỏi cuốn chay  ",
        "Không tôm thịt",
        _newCategory.Id,
        25, 0, 2, "Medium",
        Instructions: null,
        Nutrition: nutrition);

    [Fact(DisplayName = "FR-RCP-004: owner sửa → lưu, slug giữ nguyên (D13), trả DTO với category mới")]
    public async Task Handle_Owner_UpdatesAndKeepsSlug()
    {
        var command = Command();

        var dto = await Handler().Handle(command, CancellationToken.None);

        _recipe.Title.Should().Be("Gỏi cuốn chay");
        _recipe.Slug.Should().Be("goi-cuon-tom-thit");
        _recipe.Instructions.Should().BeNull("D69 — PUT thay toàn bộ, instructions null = xóa");
        _recipe.CategoryId.Should().Be(_newCategory.Id);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        dto.Category.Name.Should().Be("Món chay");
        dto.Slug.Should().Be("goi-cuon-tom-thit");
        command.TagsToInvalidate.Should().BeEquivalentTo(["recipes", "recipe:goi-cuon-tom-thit"]);
    }

    [Fact(DisplayName = "FR-RCP-004 bước 8/D69: nutrition null → giữ nguyên; có giá trị → thay")]
    public async Task Handle_Nutrition_KeepOrReplace()
    {
        await Handler().Handle(Command(), CancellationToken.None);
        _recipe.Nutrition.Calories.Should().Be(250);

        await Handler().Handle(Command(nutrition: new RecipeNutritionInput(100, null, null, null, null, null)), CancellationToken.None);
        _recipe.Nutrition.Calories.Should().Be(100);
        _recipe.Nutrition.Protein.Should().BeNull();
    }

    [Fact(DisplayName = "FR-RCP-004/A3: recipe không tồn tại (hoặc đã xóa mềm) → 404 RECIPE_NOT_FOUND")]
    public async Task Handle_NotFound_Throws404()
    {
        _recipes.GetByIdWithDetailsAsync(_recipe.Id, Arg.Any<CancellationToken>()).Returns((Recipe?)null);

        var act = () => Handler().Handle(Command(), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeNotFound);
    }

    [Fact(DisplayName = "FR-RCP-004/A1, NFR-SEC-006: Author khác → 403 RECIPE_FORBIDDEN, không lưu")]
    public async Task Handle_OtherAuthor_Throws403()
    {
        _currentUser.UserId.Returns("someone-else");

        var act = () => Handler().Handle(Command(), CancellationToken.None);

        (await act.Should().ThrowAsync<ForbiddenException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeForbidden);
        _recipe.Title.Should().Be("Gỏi cuốn tôm thịt");
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact(DisplayName = "FR-RCP-004: Admin sửa recipe của người khác → được (bypass ownership)")]
    public async Task Handle_Admin_Allowed()
    {
        _currentUser.UserId.Returns("admin-1");
        _currentUser.IsAdmin.Returns(true);

        await Handler().Handle(Command(), CancellationToken.None);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-RCP-004/A2, D4/D34/D71: If-Match lệch RowVersion → 409 RECIPE_CONCURRENCY_CONFLICT, không lưu")]
    public async Task Handle_StaleRowVersion_Throws409()
    {
        var act = () => Handler().Handle(Command(rowVersion: [9, 9, 9, 9]), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeConcurrencyConflict);
        _recipe.Title.Should().Be("Gỏi cuốn tôm thịt");
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact(DisplayName = "FR-RCP-004: kiểm tra quyền TRƯỚC concurrency — người lạ không dò được RowVersion qua 409")]
    public async Task Handle_OtherAuthorWithStaleRowVersion_Gets403Not409()
    {
        _currentUser.UserId.Returns("someone-else");

        var act = () => Handler().Handle(Command(rowVersion: [9, 9]), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
