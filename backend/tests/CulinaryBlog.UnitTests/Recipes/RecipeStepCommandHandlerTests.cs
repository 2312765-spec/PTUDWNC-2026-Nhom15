using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Application.Recipes.Commands.Steps;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Exceptions;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-RCP-010 — Add/Update/Delete step: D6 (server đánh số), D31 (AddAsync tường minh), D70, D73.</summary>
public class RecipeStepCommandHandlerTests
{
    private const string OwnerId = "owner-1";
    private readonly IRecipeRepository _recipes = Substitute.For<IRecipeRepository>();
    private readonly IRepository<RecipeStep> _stepRepository = Substitute.For<IRepository<RecipeStep>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly Recipe _recipe;
    private readonly RecipeStep[] _steps;

    public RecipeStepCommandHandlerTests()
    {
        _recipe = Recipe.Create("Bánh xèo", "banh-xeo", "Bánh xèo miền Tây", 20, 30, 4,
            RecipeDifficulty.Medium, Guid.NewGuid(), OwnerId);
        _steps = [.. Enumerable.Range(1, 3).Select(i => _recipe.AppendStep($"Bước {i}", $"Mô tả {i}", null, null))];

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

    private AddRecipeStepCommandHandler AddHandler() => new(_recipes, _stepRepository, _unitOfWork, Authorization());

    private UpdateRecipeStepCommandHandler UpdateHandler() => new(_recipes, _unitOfWork, Authorization());

    private DeleteRecipeStepCommandHandler DeleteHandler() => new(_recipes, _stepRepository, _unitOfWork, Authorization());

    [Fact(DisplayName = "FR-RCP-010/D6/D31: thêm bước → StepNumber = Max + 1, gọi AddAsync tường minh rồi SaveChanges")]
    public async Task Add_AppendsWithNextNumber()
    {
        var command = new AddRecipeStepCommand(_recipe.Id, " Lật bánh ", " Lật khi vàng giòn ", 3, null);

        var dto = await AddHandler().Handle(command, CancellationToken.None);

        dto.StepNumber.Should().Be(4);
        dto.Title.Should().Be("Lật bánh");
        dto.Description.Should().Be("Lật khi vàng giòn");
        dto.TimerMinutes.Should().Be(3);
        await _stepRepository.Received(1).AddAsync(Arg.Is<RecipeStep>(s => s.Id == dto.Id), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        command.TagsToInvalidate.Should().BeEquivalentTo(["recipes", "recipe:banh-xeo"]);
    }

    [Fact(DisplayName = "FR-RCP-010/A1: recipe không tồn tại → 404 RECIPE_NOT_FOUND")]
    public async Task Add_RecipeNotFound_Throws404()
    {
        var act = () => AddHandler().Handle(new AddRecipeStepCommand(Guid.NewGuid(), "Bước", "Mô tả", null, null), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeNotFound);
    }

    [Fact(DisplayName = "FR-RCP-010/A2, NFR-SEC-006: Author khác → 403 RECIPE_FORBIDDEN, không thêm bước")]
    public async Task Add_OtherAuthor_Throws403()
    {
        _currentUser.UserId.Returns("someone-else");

        var act = () => AddHandler().Handle(new AddRecipeStepCommand(_recipe.Id, "Bước", "Mô tả", null, null), CancellationToken.None);

        (await act.Should().ThrowAsync<ForbiddenException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeForbidden);
        _recipe.Steps.Should().HaveCount(3);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact(DisplayName = "FR-RCP-010/D6: sửa + đổi vị trí → lưu qua SaveStepsAsync (2 pha, D73), trả toàn bộ bước theo thứ tự mới")]
    public async Task Update_MovesAndReturnsAllSteps()
    {
        var command = new UpdateRecipeStepCommand(_recipe.Id, _steps[2].Id, "Bước 3 sửa", "Mô tả mới", 7, null, StepNumber: 1);

        var result = await UpdateHandler().Handle(command, CancellationToken.None);

        result.Select(s => s.Title).Should().Equal("Bước 3 sửa", "Bước 1", "Bước 2");
        result.Select(s => s.StepNumber).Should().Equal(1, 2, 3);
        await _recipes.Received(1).SaveStepsAsync(_recipe, Arg.Any<CancellationToken>());
        command.TagsToInvalidate.Should().BeEquivalentTo(["recipes", "recipe:banh-xeo"]);
    }

    [Fact(DisplayName = "FR-RCP-010/D73: stepId không thuộc recipe → 404 RECIPE_STEP_NOT_FOUND")]
    public async Task Update_UnknownStep_Throws404()
    {
        var command = new UpdateRecipeStepCommand(_recipe.Id, Guid.NewGuid(), "Bước", "Mô tả", null, null, null);

        var act = () => UpdateHandler().Handle(command, CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeStepNotFound);
    }

    [Fact(DisplayName = "FR-RCP-010/D73: stepNumber vượt số bước → DomainException VALIDATION_ERROR (400), không lưu")]
    public async Task Update_PositionOutOfRange_Throws()
    {
        var command = new UpdateRecipeStepCommand(_recipe.Id, _steps[0].Id, "Bước", "Mô tả", null, null, StepNumber: 9);

        var act = () => UpdateHandler().Handle(command, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.ErrorCode.Should().Be(ErrorCodes.ValidationError);
        await _recipes.DidNotReceiveWithAnyArgs().SaveStepsAsync(default!, default);
    }

    [Fact(DisplayName = "FR-RCP-010/D73: xóa bước → hard delete qua repository + renumber, lưu qua SaveStepsAsync")]
    public async Task Delete_RemovesAndRenumbers()
    {
        var command = new DeleteRecipeStepCommand(_recipe.Id, _steps[0].Id);

        await DeleteHandler().Handle(command, CancellationToken.None);

        _stepRepository.Received(1).Delete(_steps[0]);
        _recipe.Steps.OrderBy(s => s.StepNumber).Select(s => s.Title).Should().Equal("Bước 2", "Bước 3");
        _recipe.Steps.Select(s => s.StepNumber).Should().BeEquivalentTo([1, 2]);
        await _recipes.Received(1).SaveStepsAsync(_recipe, Arg.Any<CancellationToken>());
        command.TagsToInvalidate.Should().BeEquivalentTo(["recipes", "recipe:banh-xeo"]);
    }

    [Fact(DisplayName = "FR-RCP-010: Admin xóa bước recipe của người khác → được")]
    public async Task Delete_AsAdmin_Allowed()
    {
        _currentUser.UserId.Returns("admin-1");
        _currentUser.IsAdmin.Returns(true);

        await DeleteHandler().Handle(new DeleteRecipeStepCommand(_recipe.Id, _steps[1].Id), CancellationToken.None);

        _recipe.Steps.Should().HaveCount(2);
    }

    [Fact(DisplayName = "FR-RCP-010/A2: Author khác xóa bước → 403, bước còn nguyên")]
    public async Task Delete_OtherAuthor_Throws403()
    {
        _currentUser.UserId.Returns("someone-else");

        var act = () => DeleteHandler().Handle(new DeleteRecipeStepCommand(_recipe.Id, _steps[0].Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        _recipe.Steps.Should().HaveCount(3);
        _stepRepository.DidNotReceiveWithAnyArgs().Delete(default!);
    }

    [Fact(DisplayName = "FR-RCP-010/D73: xóa bước không tồn tại → 404 RECIPE_STEP_NOT_FOUND")]
    public async Task Delete_UnknownStep_Throws404()
    {
        var act = () => DeleteHandler().Handle(new DeleteRecipeStepCommand(_recipe.Id, Guid.NewGuid()), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeStepNotFound);
    }
}
