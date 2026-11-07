using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Application.Recipes.Commands.Archive;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-RCP-006 — domain Archive + handler + validator: D1, D70, D77, D79 (idempotent, If-Match, PublishedAt).</summary>
public sealed class ArchiveRecipeCommandTests
{
    private const string OwnerId = "owner-1";
    private readonly IRecipeRepository _recipes = Substitute.For<IRecipeRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly Category _category = Category.Create("Món cuốn", "mon-cuon");
    private readonly Recipe _recipe;

    public ArchiveRecipeCommandTests()
    {
        _recipe = NewRecipe();
        _recipe.RowVersion = [1, 2, 3, 4];

        _recipes.GetByIdWithDetailsForUpdateAsync(_recipe.Id, Arg.Any<CancellationToken>()).Returns(_recipe);
        _categories.GetByIdAsync(_category.Id, Arg.Any<CancellationToken>()).Returns(_category);
        _identity.GetUserByIdAsync(OwnerId, Arg.Any<CancellationToken>())
            .Returns(new AuthenticatedUser(OwnerId, "a@x.vn", "Tác giả A", null, null, [Roles.Author]));
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(OwnerId);
        _currentUser.IsInRole(Roles.Author).Returns(true);

        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>()));
    }

    private Recipe NewRecipe()
    {
        var recipe = Recipe.Create("Bánh xèo", "banh-xeo", "Bánh xèo miền Tây", 20, 30, 4,
            RecipeDifficulty.Medium, _category.Id, OwnerId);
        recipe.AppendStep("Bước 1", "Mô tả", null, null);
        recipe.AddIngredient("Bột gạo", null, null);
        return recipe;
    }

    private ArchiveRecipeCommandHandler Handler() => new(
        _recipes, _categories, _unitOfWork, _identity, new RecipeAuthorizationHandler(_currentUser), _currentUser,
        NullLogger<ArchiveRecipeCommandHandler>.Instance);

    // ---- Domain ----

    [Fact(DisplayName = "FR-RCP-006/D79: Draft → Archived (true), step/ingredient còn nguyên (D1)")]
    public void Domain_ArchiveDraft()
    {
        var recipe = NewRecipe();

        recipe.Archive().Should().BeTrue();

        recipe.Status.Should().Be(RecipeStatus.Archived);
        recipe.Steps.Should().HaveCount(1);
        recipe.Ingredients.Should().HaveCount(1);
        recipe.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "FR-RCP-006/D79: Published → Archived (true), giữ PublishedAt")]
    public void Domain_ArchivePublished_KeepsPublishedAt()
    {
        var recipe = NewRecipe();
        recipe.Publish();
        var publishedAt = recipe.PublishedAt;

        recipe.Archive().Should().BeTrue();

        recipe.Status.Should().Be(RecipeStatus.Archived);
        recipe.PublishedAt.Should().Be(publishedAt);
    }

    [Fact(DisplayName = "FR-RCP-006/D79: Archive hai lần → lần 2 trả false, UpdatedAt không đổi")]
    public void Domain_ArchiveTwice_IsIdempotent()
    {
        var recipe = NewRecipe();
        recipe.Archive();
        var updatedAt = recipe.UpdatedAt;

        recipe.Archive().Should().BeFalse();

        recipe.Status.Should().Be(RecipeStatus.Archived);
        recipe.UpdatedAt.Should().Be(updatedAt);
    }

    // ---- Handler ----

    [Fact(DisplayName = "FR-RCP-006/D8: archive thành công → SaveChanges 1 lần, xóa cache recipe, trả DTO Archived")]
    public async Task Handler_Archive_Saves()
    {
        var command = new ArchiveRecipeCommand(_recipe.Id);

        var dto = await Handler().Handle(command, CancellationToken.None);

        dto.Status.Should().Be((short)RecipeStatus.Archived);
        dto.Category.Name.Should().Be("Món cuốn");
        dto.Author.DisplayName.Should().Be("Tác giả A");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        command.TagsToInvalidate.Should().BeEquivalentTo(["recipes", "recipe:banh-xeo"]);
    }

    [Fact(DisplayName = "FR-RCP-006/D79: recipe đã Archived → không SaveChanges, vẫn trả 200 DTO")]
    public async Task Handler_AlreadyArchived_IsNoOp()
    {
        _recipe.Archive();

        var dto = await Handler().Handle(new ArchiveRecipeCommand(_recipe.Id), CancellationToken.None);

        dto.Status.Should().Be((short)RecipeStatus.Archived);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact(DisplayName = "FR-RCP-006/D4/D79: If-Match lệch RowVersion → 409 RECIPE_CONCURRENCY_CONFLICT, không đổi trạng thái")]
    public async Task Handler_StaleIfMatch_Throws409()
    {
        var stale = $"\"{Convert.ToBase64String([9, 9, 9, 9])}\"";

        var act = () => Handler().Handle(new ArchiveRecipeCommand(_recipe.Id, stale), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeConcurrencyConflict);
        _recipe.Status.Should().Be(RecipeStatus.Draft);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact(DisplayName = "FR-RCP-006/D79: If-Match khớp RowVersion → archive được")]
    public async Task Handler_MatchingIfMatch_Archives()
    {
        var current = $"W/\"{Convert.ToBase64String(_recipe.RowVersion)}\"";

        var dto = await Handler().Handle(new ArchiveRecipeCommand(_recipe.Id, current), CancellationToken.None);

        dto.Status.Should().Be((short)RecipeStatus.Archived);
    }

    [Fact(DisplayName = "FR-RCP-006/A2/NFR-SEC-006: Author khác → 403 RECIPE_FORBIDDEN (trước cả kiểm tra If-Match)")]
    public async Task Handler_OtherAuthor_Throws403()
    {
        _currentUser.UserId.Returns("someone-else");

        var act = () => Handler().Handle(new ArchiveRecipeCommand(_recipe.Id, "\"AAAA\""), CancellationToken.None);

        (await act.Should().ThrowAsync<ForbiddenException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeForbidden);
        _recipe.Status.Should().Be(RecipeStatus.Draft);
    }

    [Fact(DisplayName = "FR-RCP-006: Admin archive recipe của Author khác → được")]
    public async Task Handler_Admin_Archives()
    {
        _currentUser.UserId.Returns("admin-1");
        _currentUser.IsAdmin.Returns(true);
        _currentUser.IsInRole(Roles.Admin).Returns(true);

        var dto = await Handler().Handle(new ArchiveRecipeCommand(_recipe.Id), CancellationToken.None);

        dto.Status.Should().Be((short)RecipeStatus.Archived);
    }

    [Fact(DisplayName = "FR-RCP-006/A1: recipe không tồn tại → 404 RECIPE_NOT_FOUND")]
    public async Task Handler_NotFound_Throws404()
    {
        var act = () => Handler().Handle(new ArchiveRecipeCommand(Guid.NewGuid()), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeNotFound);
    }

    // ---- Validator ----

    [Theory(DisplayName = "FR-RCP-006/D79: If-Match vắng hoặc đúng định dạng → hợp lệ; sai định dạng → lỗi trường RowVersion")]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("\"AQIDBA==\"", true)]
    [InlineData("*", false)]
    [InlineData("\"khong-phai-base64!\"", false)]
    public void Validator_IfMatch(string? ifMatch, bool valid)
    {
        var result = new ArchiveRecipeCommandValidator().Validate(new ArchiveRecipeCommand(Guid.NewGuid(), ifMatch));

        result.IsValid.Should().Be(valid);
        if (!valid)
        {
            result.Errors.Should().Contain(e => e.PropertyName == "RowVersion");
        }
    }
}
