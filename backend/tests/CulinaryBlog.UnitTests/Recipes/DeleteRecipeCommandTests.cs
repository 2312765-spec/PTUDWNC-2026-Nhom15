using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Application.Recipes.Commands.Delete;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-RCP-007 — D1 (soft delete qua repository), D70, D78 (If-Match tùy chọn, thứ tự 404 → 403 → 409).</summary>
public class DeleteRecipeCommandTests
{
    private const string OwnerId = "owner-1";
    private readonly IRecipeRepository _recipes = Substitute.For<IRecipeRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly Recipe _recipe;

    public DeleteRecipeCommandTests()
    {
        _recipe = Recipe.Create("Bánh xèo", "banh-xeo", "Bánh xèo miền Tây", 20, 30, 4,
            RecipeDifficulty.Medium, Guid.NewGuid(), OwnerId, RecipeStatus.Published);
        _recipe.RowVersion = [1, 2, 3, 4];

        _recipes.GetByIdWithDetailsForUpdateAsync(_recipe.Id, Arg.Any<CancellationToken>()).Returns(_recipe);
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(OwnerId);
        _currentUser.IsInRole(Roles.Author).Returns(true);

        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>()));
    }

    private DeleteRecipeCommandHandler Handler() => new(
        _recipes, _unitOfWork, new RecipeAuthorizationHandler(_currentUser), _currentUser,
        NullLogger<DeleteRecipeCommandHandler>.Instance);

    [Fact(DisplayName = "FR-RCP-007/D1: owner xóa → IRecipeRepository.Delete (soft delete) rồi SaveChanges, xóa cache recipe")]
    public async Task Delete_Owner_SoftDeletes()
    {
        var command = new DeleteRecipeCommand(_recipe.Id);

        await Handler().Handle(command, CancellationToken.None);

        _recipes.Received(1).Delete(_recipe);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        command.TagsToInvalidate.Should().BeEquivalentTo(["recipes", "recipe:banh-xeo"]);
    }

    [Fact(DisplayName = "FR-RCP-007: Admin xóa recipe của người khác → được")]
    public async Task Delete_Admin_Allowed()
    {
        _currentUser.UserId.Returns("admin-1");
        _currentUser.IsAdmin.Returns(true);

        await Handler().Handle(new DeleteRecipeCommand(_recipe.Id), CancellationToken.None);

        _recipes.Received(1).Delete(_recipe);
    }

    [Fact(DisplayName = "FR-RCP-007/A1: recipe không tồn tại (hoặc đã xóa mềm) → 404 RECIPE_NOT_FOUND")]
    public async Task Delete_NotFound_Throws404()
    {
        var act = () => Handler().Handle(new DeleteRecipeCommand(Guid.NewGuid()), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeNotFound);
    }

    [Fact(DisplayName = "FR-RCP-007/A2, NFR-SEC-006: Author khác → 403 RECIPE_FORBIDDEN, không xóa")]
    public async Task Delete_OtherAuthor_Throws403()
    {
        _currentUser.UserId.Returns("someone-else");

        var act = () => Handler().Handle(new DeleteRecipeCommand(_recipe.Id, "\"AAAA\""), CancellationToken.None);

        (await act.Should().ThrowAsync<ForbiddenException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeForbidden);
        _recipes.DidNotReceiveWithAnyArgs().Delete(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact(DisplayName = "FR-RCP-007/D4/D78: If-Match lệch RowVersion → 409 RECIPE_CONCURRENCY_CONFLICT, không xóa")]
    public async Task Delete_StaleIfMatch_Throws409()
    {
        var act = () => Handler().Handle(
            new DeleteRecipeCommand(_recipe.Id, $"\"{Convert.ToBase64String([9, 9, 9, 9])}\""), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeConcurrencyConflict);
        _recipes.DidNotReceiveWithAnyArgs().Delete(default!);
    }

    [Fact(DisplayName = "FR-RCP-007/D78: If-Match khớp RowVersion → xóa")]
    public async Task Delete_MatchingIfMatch_Deletes()
    {
        await Handler().Handle(
            new DeleteRecipeCommand(_recipe.Id, $"\"{Convert.ToBase64String(_recipe.RowVersion)}\""), CancellationToken.None);

        _recipes.Received(1).Delete(_recipe);
    }

    [Theory(DisplayName = "FR-RCP-007/D78: If-Match vắng hoặc đúng định dạng → hợp lệ; sai định dạng → lỗi trường RowVersion")]
    [InlineData(null, true)]
    [InlineData("  ", true)]
    [InlineData("\"AQIDBA==\"", true)]
    [InlineData("*", false)]
    [InlineData("\"%%%\"", false)]
    public void Validator_IfMatch(string? ifMatch, bool valid)
    {
        var result = new DeleteRecipeCommandValidator().Validate(new DeleteRecipeCommand(Guid.NewGuid(), ifMatch));

        result.IsValid.Should().Be(valid);
        if (!valid)
        {
            result.Errors.Should().Contain(e => e.PropertyName == "RowVersion");
        }
    }
}
