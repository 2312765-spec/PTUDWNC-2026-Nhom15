using System.Diagnostics.Metrics;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Observability;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Application.Recipes.Commands.Publish;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Exceptions;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-RCP-005 — domain Publish/Unpublish + handler: D3, D39 (metric), D70, D77 (idempotent, If-Match, PublishedAt).</summary>
public sealed class PublishRecipeCommandTests : IDisposable
{
    private const string OwnerId = "owner-1";
    private readonly IRecipeRepository _recipes = Substitute.For<IRecipeRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly TestMeterFactory _meterFactory = new();
    private readonly Category _category = Category.Create("Món cuốn", "mon-cuon");
    private readonly Recipe _recipe;

    public PublishRecipeCommandTests()
    {
        _recipe = NewRecipe(steps: 1, ingredients: 1);
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

    private Recipe NewRecipe(int steps, int ingredients, RecipeStatus status = RecipeStatus.Draft)
    {
        var recipe = Recipe.Create("Bánh xèo", "banh-xeo", "Bánh xèo miền Tây", 20, 30, 4,
            RecipeDifficulty.Medium, _category.Id, OwnerId, status);
        for (var i = 0; i < steps; i++)
        {
            recipe.AppendStep($"Bước {i + 1}", "Mô tả", null, null);
        }

        for (var i = 0; i < ingredients; i++)
        {
            recipe.AddIngredient($"NL {i + 1}", null, null);
        }

        return recipe;
    }

    private PublishRecipeCommandHandler Handler() => new(
        _recipes, _categories, _unitOfWork, _identity, new RecipeAuthorizationHandler(_currentUser), _currentUser,
        new RecipeMetrics(_meterFactory), NullLogger<PublishRecipeCommandHandler>.Instance);

    private MetricCollector<long> PublishedCollector() =>
        new(_meterFactory, RecipeMetrics.MeterName, RecipeMetrics.PublishedCounterName);

    // ---- Domain ----

    [Theory(DisplayName = "FR-RCP-005/D3: thiếu step hoặc thiếu ingredient → DomainException RECIPE_PUBLISH_INCOMPLETE, vẫn Draft")]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    public void Domain_PublishIncomplete_Throws(int steps, int ingredients)
    {
        var recipe = NewRecipe(steps, ingredients);

        var act = () => recipe.Publish();

        act.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("RECIPE_PUBLISH_INCOMPLETE");
        recipe.Status.Should().Be(RecipeStatus.Draft);
        recipe.PublishedAt.Should().BeNull();
    }

    [Fact(DisplayName = "FR-RCP-005/D77: Publish hai lần → lần 2 trả false, không đổi PublishedAt")]
    public void Domain_PublishTwice_IsIdempotent()
    {
        var recipe = NewRecipe(1, 1);

        recipe.Publish().Should().BeTrue();
        var publishedAt = recipe.PublishedAt;
        recipe.Publish().Should().BeFalse();

        recipe.Status.Should().Be(RecipeStatus.Published);
        recipe.PublishedAt.Should().Be(publishedAt);
    }

    [Fact(DisplayName = "FR-RCP-005/D77: Unpublish rồi Publish lại → giữ PublishedAt lần đầu")]
    public void Domain_Republish_KeepsFirstPublishedAt()
    {
        var recipe = NewRecipe(1, 1);
        recipe.Publish();
        var publishedAt = recipe.PublishedAt;

        recipe.Unpublish().Should().BeTrue();
        recipe.Status.Should().Be(RecipeStatus.Draft);
        recipe.Publish();

        recipe.PublishedAt.Should().Be(publishedAt);
    }

    [Theory(DisplayName = "FR-RCP-005/D77: Unpublish — Published/Archived → Draft (true), Draft → false")]
    [InlineData(RecipeStatus.Published, true)]
    [InlineData(RecipeStatus.Archived, true)]
    [InlineData(RecipeStatus.Draft, false)]
    public void Domain_Unpublish(RecipeStatus from, bool expectedChanged)
    {
        var recipe = NewRecipe(1, 1, from);

        recipe.Unpublish().Should().Be(expectedChanged);

        recipe.Status.Should().Be(RecipeStatus.Draft);
    }

    [Fact(DisplayName = "FR-RCP-005/D77: Archived đủ điều kiện → Publish được")]
    public void Domain_PublishArchived_Succeeds()
    {
        var recipe = NewRecipe(1, 1);
        recipe.Archive();

        recipe.Publish().Should().BeTrue();

        recipe.Status.Should().Be(RecipeStatus.Published);
    }

    // ---- Handler ----

    [Fact(DisplayName = "FR-RCP-005/D39: publish thành công → SaveChanges, đếm metric published đúng 1 lần, xóa cache recipe")]
    public async Task Handler_Publish_SavesAndRecordsMetric()
    {
        using var collector = PublishedCollector();
        var command = new PublishRecipeCommand(_recipe.Id, IsPublish: true);

        var dto = await Handler().Handle(command, CancellationToken.None);

        dto.Status.Should().Be((short)RecipeStatus.Published);
        dto.Category.Name.Should().Be("Món cuốn");
        dto.Author.DisplayName.Should().Be("Tác giả A");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        collector.GetMeasurementSnapshot().Should().ContainSingle().Which.Value.Should().Be(1);
        command.TagsToInvalidate.Should().BeEquivalentTo(["recipes", "recipe:banh-xeo"]);
    }

    [Fact(DisplayName = "FR-RCP-005/D3/D39: publish bị D3 chặn → không SaveChanges, không đếm metric")]
    public async Task Handler_PublishIncomplete_DoesNotRecordMetric()
    {
        using var collector = PublishedCollector();
        var incomplete = NewRecipe(steps: 0, ingredients: 1);
        _recipes.GetByIdWithDetailsForUpdateAsync(incomplete.Id, Arg.Any<CancellationToken>()).Returns(incomplete);

        var act = () => Handler().Handle(new PublishRecipeCommand(incomplete.Id, true), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipePublishIncomplete);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        collector.GetMeasurementSnapshot().Should().BeEmpty();
    }

    [Fact(DisplayName = "FR-RCP-005/A2/D77: publish recipe đã Published → không SaveChanges, không đếm metric")]
    public async Task Handler_PublishAlreadyPublished_IsNoOp()
    {
        using var collector = PublishedCollector();
        _recipe.Publish();

        var dto = await Handler().Handle(new PublishRecipeCommand(_recipe.Id, true), CancellationToken.None);

        dto.Status.Should().Be((short)RecipeStatus.Published);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        collector.GetMeasurementSnapshot().Should().BeEmpty();
    }

    [Fact(DisplayName = "FR-RCP-005/D39: unpublish → SaveChanges nhưng KHÔNG đếm metric published")]
    public async Task Handler_Unpublish_DoesNotRecordMetric()
    {
        using var collector = PublishedCollector();
        _recipe.Publish();

        var dto = await Handler().Handle(new PublishRecipeCommand(_recipe.Id, false), CancellationToken.None);

        dto.Status.Should().Be((short)RecipeStatus.Draft);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        collector.GetMeasurementSnapshot().Should().BeEmpty();
    }

    [Fact(DisplayName = "FR-RCP-005/D4/D77: If-Match lệch RowVersion → 409 RECIPE_CONCURRENCY_CONFLICT, không đổi trạng thái")]
    public async Task Handler_StaleIfMatch_Throws409()
    {
        var stale = $"\"{Convert.ToBase64String([9, 9, 9, 9])}\"";

        var act = () => Handler().Handle(new PublishRecipeCommand(_recipe.Id, true, stale), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeConcurrencyConflict);
        _recipe.Status.Should().Be(RecipeStatus.Draft);
    }

    [Fact(DisplayName = "FR-RCP-005/D77: If-Match khớp RowVersion → publish được")]
    public async Task Handler_MatchingIfMatch_Publishes()
    {
        var current = $"W/\"{Convert.ToBase64String(_recipe.RowVersion)}\"";

        var dto = await Handler().Handle(new PublishRecipeCommand(_recipe.Id, true, current), CancellationToken.None);

        dto.Status.Should().Be((short)RecipeStatus.Published);
    }

    [Fact(DisplayName = "FR-RCP-005/NFR-SEC-006: Author khác → 403 RECIPE_FORBIDDEN (trước cả kiểm tra If-Match)")]
    public async Task Handler_OtherAuthor_Throws403()
    {
        _currentUser.UserId.Returns("someone-else");

        var act = () => Handler().Handle(new PublishRecipeCommand(_recipe.Id, true, "\"AAAA\""), CancellationToken.None);

        (await act.Should().ThrowAsync<ForbiddenException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeForbidden);
        _recipe.Status.Should().Be(RecipeStatus.Draft);
    }

    [Fact(DisplayName = "FR-RCP-005: recipe không tồn tại → 404 RECIPE_NOT_FOUND")]
    public async Task Handler_NotFound_Throws404()
    {
        var act = () => Handler().Handle(new PublishRecipeCommand(Guid.NewGuid(), true), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeNotFound);
    }

    [Theory(DisplayName = "FR-RCP-005/D77: If-Match vắng hoặc đúng định dạng → hợp lệ; sai định dạng → lỗi trường RowVersion")]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("\"AQIDBA==\"", true)]
    [InlineData("*", false)]
    [InlineData("\"khong-phai-base64!\"", false)]
    public void Validator_IfMatch(string? ifMatch, bool valid)
    {
        var result = new PublishRecipeCommandValidator().Validate(new PublishRecipeCommand(Guid.NewGuid(), true, ifMatch));

        result.IsValid.Should().Be(valid);
        if (!valid)
        {
            result.Errors.Should().Contain(e => e.PropertyName == "RowVersion");
        }
    }

    public void Dispose() => _meterFactory.Dispose();

    /// <summary>IMeterFactory riêng cho lớp test — MetricCollector chỉ nghe meter tạo từ đây.</summary>
    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(new MeterOptions(options.Name) { Version = options.Version, Tags = options.Tags, Scope = this });
            _meters.Add(meter);
            return meter;
        }

        public void Dispose() => _meters.ForEach(m => m.Dispose());
    }
}
