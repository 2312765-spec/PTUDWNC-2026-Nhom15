using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Application.Recipes.Commands.Ingredients;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Exceptions;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-RCP-009 — Add/Update/Delete ingredient: D7, D31 (AddAsync tường minh), D70, D76; validator D7.</summary>
public class RecipeIngredientCommandTests
{
    private const string OwnerId = "owner-1";
    private readonly IRecipeRepository _recipes = Substitute.For<IRecipeRepository>();
    private readonly IRepository<RecipeIngredient> _ingredientRepository = Substitute.For<IRepository<RecipeIngredient>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly Recipe _recipe;
    private readonly RecipeIngredient[] _ingredients;

    public RecipeIngredientCommandTests()
    {
        _recipe = Recipe.Create("Bánh xèo", "banh-xeo", "Bánh xèo miền Tây", 20, 30, 4,
            RecipeDifficulty.Medium, Guid.NewGuid(), OwnerId);
        _ingredients = [.. Enumerable.Range(1, 3).Select(i => _recipe.AddIngredient($"NL {i}", i, "g"))];

        _recipes.GetByIdWithDetailsForUpdateAsync(_recipe.Id, Arg.Any<CancellationToken>()).Returns(_recipe);
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(OwnerId);
        _currentUser.IsInRole(Roles.Author).Returns(true);

        // Chạy khối transaction thật (giả lập không retry) để kiểm tra logic bên trong.
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>()));
    }

    private RecipeAuthorizationHandler Authorization() => new(_currentUser);

    private AddRecipeIngredientCommandHandler AddHandler() => new(_recipes, _ingredientRepository, _unitOfWork, Authorization());

    private UpdateRecipeIngredientCommandHandler UpdateHandler() => new(_recipes, _unitOfWork, Authorization());

    private DeleteRecipeIngredientCommandHandler DeleteHandler() => new(_recipes, _ingredientRepository, _unitOfWork, Authorization());

    [Fact(DisplayName = "FR-RCP-009/D7/D31: thêm nguyên liệu → cắt khoảng trắng, chuỗi rỗng = null, AddAsync tường minh rồi SaveChanges")]
    public async Task Add_AppendsAndPersists()
    {
        var command = new AddRecipeIngredientCommand(_recipe.Id, " Hành lá ", null, "  ", " thái nhỏ ", null);

        var dto = await AddHandler().Handle(command, CancellationToken.None);

        dto.Name.Should().Be("Hành lá");
        dto.Quantity.Should().BeNull();
        dto.Unit.Should().BeNull();
        dto.Notes.Should().Be("thái nhỏ");
        dto.OrderIndex.Should().Be(3);
        await _ingredientRepository.Received(1).AddAsync(Arg.Is<RecipeIngredient>(i => i.Id == dto.Id), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        command.TagsToInvalidate.Should().BeEquivalentTo(["recipes", "recipe:banh-xeo"]);
    }

    [Fact(DisplayName = "FR-RCP-009/A1: recipe không tồn tại → 404 RECIPE_NOT_FOUND")]
    public async Task Add_RecipeNotFound_Throws404()
    {
        var act = () => AddHandler().Handle(new AddRecipeIngredientCommand(Guid.NewGuid(), "Muối", null, null, null, null), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeNotFound);
    }

    [Fact(DisplayName = "FR-RCP-009/A2, NFR-SEC-006: Author khác → 403 RECIPE_FORBIDDEN, không thêm")]
    public async Task Add_OtherAuthor_Throws403()
    {
        _currentUser.UserId.Returns("someone-else");

        var act = () => AddHandler().Handle(new AddRecipeIngredientCommand(_recipe.Id, "Muối", null, null, null, null), CancellationToken.None);

        (await act.Should().ThrowAsync<ForbiddenException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeForbidden);
        _recipe.Ingredients.Should().HaveCount(3);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact(DisplayName = "FR-RCP-009/D76: sửa + đổi vị trí → trả toàn bộ nguyên liệu theo thứ tự mới")]
    public async Task Update_MovesAndReturnsAll()
    {
        var command = new UpdateRecipeIngredientCommand(_recipe.Id, _ingredients[2].Id, "NL 3 sửa", 5m, "ml", null, OrderIndex: 0);

        var result = await UpdateHandler().Handle(command, CancellationToken.None);

        result.Select(i => i.Name).Should().Equal("NL 3 sửa", "NL 1", "NL 2");
        result.Select(i => i.OrderIndex).Should().Equal(0, 1, 2);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        command.TagsToInvalidate.Should().BeEquivalentTo(["recipes", "recipe:banh-xeo"]);
    }

    [Fact(DisplayName = "FR-RCP-009/D76: ingredientId không thuộc recipe → 404 RECIPE_INGREDIENT_NOT_FOUND")]
    public async Task Update_UnknownIngredient_Throws404()
    {
        var command = new UpdateRecipeIngredientCommand(_recipe.Id, Guid.NewGuid(), "Muối", null, null, null, null);

        var act = () => UpdateHandler().Handle(command, CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeIngredientNotFound);
    }

    [Fact(DisplayName = "FR-RCP-009/D76: orderIndex vượt số nguyên liệu → DomainException VALIDATION_ERROR, không lưu")]
    public async Task Update_PositionOutOfRange_Throws()
    {
        var command = new UpdateRecipeIngredientCommand(_recipe.Id, _ingredients[0].Id, "Muối", null, null, null, OrderIndex: 3);

        var act = () => UpdateHandler().Handle(command, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.ErrorCode.Should().Be(ErrorCodes.ValidationError);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact(DisplayName = "FR-RCP-009/D76: xóa nguyên liệu → hard delete qua repository + đánh lại OrderIndex")]
    public async Task Delete_RemovesAndReindexes()
    {
        var command = new DeleteRecipeIngredientCommand(_recipe.Id, _ingredients[0].Id);

        await DeleteHandler().Handle(command, CancellationToken.None);

        _ingredientRepository.Received(1).Delete(_ingredients[0]);
        _recipe.Ingredients.OrderBy(i => i.OrderIndex).Select(i => i.Name).Should().Equal("NL 2", "NL 3");
        _recipe.Ingredients.Select(i => i.OrderIndex).Should().BeEquivalentTo([0, 1]);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        command.TagsToInvalidate.Should().BeEquivalentTo(["recipes", "recipe:banh-xeo"]);
    }

    [Fact(DisplayName = "FR-RCP-009: Admin xóa nguyên liệu recipe của người khác → được")]
    public async Task Delete_AsAdmin_Allowed()
    {
        _currentUser.UserId.Returns("admin-1");
        _currentUser.IsAdmin.Returns(true);

        await DeleteHandler().Handle(new DeleteRecipeIngredientCommand(_recipe.Id, _ingredients[1].Id), CancellationToken.None);

        _recipe.Ingredients.Should().HaveCount(2);
    }

    [Fact(DisplayName = "FR-RCP-009/D76: xóa nguyên liệu không tồn tại → 404 RECIPE_INGREDIENT_NOT_FOUND, không xóa gì")]
    public async Task Delete_UnknownIngredient_Throws404()
    {
        var act = () => DeleteHandler().Handle(new DeleteRecipeIngredientCommand(_recipe.Id, Guid.NewGuid()), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeIngredientNotFound);
        _ingredientRepository.DidNotReceiveWithAnyArgs().Delete(default!);
    }

    [Fact(DisplayName = "FR-RCP-009/D7: dữ liệu hợp lệ (quantity/unit null) → không lỗi")]
    public void Validator_Valid_Passes()
    {
        new AddRecipeIngredientCommandValidator()
            .Validate(new AddRecipeIngredientCommand(_recipe.Id, "Tiêu", null, null, null, null))
            .IsValid.Should().BeTrue();
    }

    public static TheoryData<string, string, decimal?, string?, string?, int?> InvalidCases => new()
    {
        { "Name", "", null, null, null, null },
        { "Name", new string('a', 201), null, null, null, null },
        { "Quantity", "Muối", 0m, null, null, null },
        { "Quantity", "Muối", -1m, null, null, null },
        { "Quantity", "Muối", 10_000_000m, null, null, null },
        { "Unit", "Muối", null, new string('a', 51), null, null },
        { "Notes", "Muối", null, null, new string('a', 501), null },
        { "OrderIndex", "Muối", null, null, null, -1 },
    };

    [Theory(DisplayName = "FR-RCP-009/D7: dữ liệu nguyên liệu sai → lỗi đúng trường (400 VALIDATION_ERROR) cho cả POST và PUT")]
    [MemberData(nameof(InvalidCases))]
    public void Validator_Invalid_FailsOnProperty(string property, string name, decimal? quantity, string? unit, string? notes, int? orderIndex)
    {
        new AddRecipeIngredientCommandValidator()
            .Validate(new AddRecipeIngredientCommand(_recipe.Id, name, quantity, unit, notes, orderIndex))
            .Errors.Should().Contain(e => e.PropertyName == property);

        new UpdateRecipeIngredientCommandValidator()
            .Validate(new UpdateRecipeIngredientCommand(_recipe.Id, Guid.NewGuid(), name, quantity, unit, notes, orderIndex))
            .Errors.Should().Contain(e => e.PropertyName == property);
    }
}
