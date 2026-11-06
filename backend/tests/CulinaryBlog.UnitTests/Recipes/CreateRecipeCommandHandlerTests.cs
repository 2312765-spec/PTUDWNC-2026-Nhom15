using System.Diagnostics.Metrics;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Common.Observability;
using CulinaryBlog.Application.Recipes.Authorization;
using CulinaryBlog.Application.Recipes.Commands;
using CulinaryBlog.Application.Recipes.Commands.Create;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using NSubstitute;
using Xunit;
using FluentValidationException = FluentValidation.ValidationException;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-RCP-003 — luồng chính, D10 (slug auto-suffix), D39 (metric), NFR-SEC-006 (quyền ở Application).</summary>
public sealed class CreateRecipeCommandHandlerTests : IDisposable
{
    private const string UserId = "author-1";
    private readonly IRecipeRepository _recipes = Substitute.For<IRecipeRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ISlugHelper _slugHelper = Substitute.For<ISlugHelper>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly TestMeterFactory _meterFactory = new();
    private readonly Category _category = Category.Create("Món cuốn", "mon-cuon");

    public CreateRecipeCommandHandlerTests()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(UserId);
        _currentUser.IsInRole(Roles.Author).Returns(true);
        _categories.GetByIdAsync(_category.Id, Arg.Any<CancellationToken>()).Returns(_category);
        _slugHelper.Generate(Arg.Any<string>()).Returns(ci => ci.Arg<string>() switch
        {
            "Gỏi cuốn tôm thịt" => "goi-cuon-tom-thit",
            "Search" => "search",
            "!!!!!" => string.Empty,
            "Bún  -  chả" => "bun---cha",
            _ => "khac",
        });
        _identity.GetUserByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new AuthenticatedUser(UserId, "a@x.vn", "Tác giả A", null, null, [Roles.Author]));
    }

    private CreateRecipeCommandHandler Handler() => new(
        _recipes, _categories, _unitOfWork, _slugHelper, _identity,
        new RecipeAuthorizationHandler(_currentUser), _currentUser, new RecipeMetrics(_meterFactory));

    private CreateRecipeCommand Command(string title = "Gỏi cuốn tôm thịt") => new(
        Title: "  " + title + "  ",
        Description: " Món cuốn tươi mát ",
        CategoryId: _category.Id,
        PrepTime: 20,
        CookTime: 0,
        Servings: 4,
        Difficulty: "hard",
        Instructions: "Cuốn chặt tay",
        Nutrition: new RecipeNutritionInput(250, 12, 30, 8, null, null),
        Steps: [new RecipeStepInput("Sơ chế", "Luộc tôm", 10, null), new RecipeStepInput("Cuốn", "Cuốn bánh", null, null)],
        Ingredients: [new RecipeIngredientInput("Tôm", 300, "g", null), new RecipeIngredientInput("Muối", null, null, "vừa đủ")]);

    [Fact(DisplayName = "FR-RCP-003: tạo Draft kèm steps/ingredients/nutrition, lưu DB, trả RecipeDetailDto")]
    public async Task Handle_Success_CreatesDraftWithChildren()
    {
        Recipe? saved = null;
        await _recipes.AddAsync(Arg.Do<Recipe>(r => saved = r), Arg.Any<CancellationToken>());

        var dto = await Handler().Handle(Command(), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Title.Should().Be("Gỏi cuốn tôm thịt", "title được trim");
        saved.Description.Should().Be("Món cuốn tươi mát");
        saved.Status.Should().Be(RecipeStatus.Draft);
        saved.AuthorId.Should().Be(UserId);
        saved.Difficulty.Should().Be(RecipeDifficulty.Hard);
        saved.Steps.Select(s => s.StepNumber).Should().Equal(1, 2);
        saved.Ingredients.Select(i => i.OrderIndex).Should().Equal(0, 1);
        saved.Nutrition.Calories.Should().Be(250);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        dto.Slug.Should().Be("goi-cuon-tom-thit");
        dto.Status.Should().Be((short)RecipeStatus.Draft);
        dto.Category.Name.Should().Be("Món cuốn");
        dto.Author.DisplayName.Should().Be("Tác giả A");
        dto.Steps.Should().HaveCount(2);
        dto.Ingredients.Should().HaveCount(2);
    }

    [Fact(DisplayName = "FR-RCP-003/D10: slug trùng (kể cả bản đã xóa mềm — D1) → thêm hậu tố -2, -3")]
    public async Task Handle_SlugTaken_AppendsSuffixFromTwo()
    {
        _recipes.ExistsBySlugAsync("goi-cuon-tom-thit", Arg.Any<CancellationToken>()).Returns(true);
        _recipes.ExistsBySlugAsync("goi-cuon-tom-thit-2", Arg.Any<CancellationToken>()).Returns(true);

        var dto = await Handler().Handle(Command(), CancellationToken.None);

        dto.Slug.Should().Be("goi-cuon-tom-thit-3");
    }

    [Fact(DisplayName = "FR-RCP-003/D10: slug cấm (search) → thêm -1 để không đụng route")]
    public async Task Handle_ReservedSlug_AppendsOne()
    {
        (await Handler().Handle(Command("Search"), CancellationToken.None)).Slug.Should().Be("search-1");
    }

    [Fact(DisplayName = "FR-RCP-003/D10/D72: gộp '-' liên tiếp; title không còn ký tự hợp lệ → slug mặc định")]
    public async Task Handle_SlugNormalization()
    {
        (await Handler().Handle(Command("Bún  -  chả"), CancellationToken.None)).Slug.Should().Be("bun-cha");
        (await Handler().Handle(Command("!!!!!"), CancellationToken.None)).Slug.Should().Be("cong-thuc");
    }

    [Fact(DisplayName = "FR-OBS-003/D39: tạo thành công → tăng counter recipes.created đúng 1")]
    public async Task Handle_Success_RecordsCreatedMetric()
    {
        using var collector = new MetricCollector<long>(_meterFactory, RecipeMetrics.MeterName, RecipeMetrics.CreatedCounterName);

        await Handler().Handle(Command(), CancellationToken.None);

        collector.GetMeasurementSnapshot().Sum(m => m.Value).Should().Be(1);
    }

    [Fact(DisplayName = "NFR-SEC-006: user không có role Author/Admin → 403 RECIPE_FORBIDDEN, không ghi DB")]
    public async Task Handle_NotAuthor_ThrowsForbidden()
    {
        _currentUser.IsInRole(Roles.Author).Returns(false);

        var act = () => Handler().Handle(Command(), CancellationToken.None);

        (await act.Should().ThrowAsync<ForbiddenException>()).Which.ErrorCode.Should().Be(ErrorCodes.RecipeForbidden);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact(DisplayName = "FR-RCP-003/D69: category biến mất giữa validator và handler → 400 VALIDATION_ERROR")]
    public async Task Handle_CategoryMissing_ThrowsValidation()
    {
        _categories.GetByIdAsync(_category.Id, Arg.Any<CancellationToken>()).Returns((Category?)null);

        var act = () => Handler().Handle(Command(), CancellationToken.None);

        (await act.Should().ThrowAsync<FluentValidationException>())
            .Which.Errors.Should().Contain(e => e.PropertyName == "CategoryId");
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
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
