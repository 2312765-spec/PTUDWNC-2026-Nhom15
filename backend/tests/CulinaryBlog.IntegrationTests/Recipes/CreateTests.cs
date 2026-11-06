using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.IntegrationTests.Common;
using CulinaryBlog.IntegrationTests.Recipes.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Recipes;

/// <summary>FR-RCP-003 — POST /api/v1/recipes. D4 (400), D10 (slug), D19 (cookTime 0), D69, D71, D72.</summary>
public sealed class CreateTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private const string Url = "/api/v1/recipes";
    private readonly RecipeCommandClient _api = new(factory);

    [Fact(DisplayName = "FR-RCP-003: Author tạo recipe kèm steps/ingredients/nutrition → 201, Draft, slug tự sinh, ETag")]
    public async Task Create_AsAuthor_Returns201Draft()
    {
        var author = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();

        var response = await _api.SendAsync(HttpMethod.Post, Url, author.Token, RecipeCommandClient.CreateBody(category.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await response.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!;
        dto.Slug.Should().StartWith("goi-cuon-tom-thit");
        dto.Status.Should().Be((short)RecipeStatus.Draft);
        dto.Author.Id.Should().Be(author.UserId);
        dto.Category.Id.Should().Be(category.Id);
        dto.Steps.Select(s => s.StepNumber).Should().Equal(1, 2);
        dto.Ingredients.Should().HaveCount(2);
        dto.Ingredients.Single(i => i.Name == "Muối").Quantity.Should().BeNull("D7 — nguyên liệu \"vừa đủ\"");
        dto.Nutrition!.Calories.Should().Be(250m);
        dto.RowVersion.Should().NotBeNullOrEmpty("D71 — client cần RowVersion để gửi If-Match khi sửa");
        response.Headers.ETag!.Tag.Should().Be($"\"{dto.RowVersion}\"");
        response.Headers.Location!.ToString().Should().Be($"/api/v1/recipes/{dto.Slug}");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var stored = await db.Recipes.Include(r => r.Steps).Include(r => r.Ingredients).SingleAsync(r => r.Id == dto.Id);
        stored.Status.Should().Be(RecipeStatus.Draft);
        stored.Steps.Should().HaveCount(2);
        stored.Ingredients.Should().HaveCount(2);
    }

    [Fact(DisplayName = "FR-RCP-003: tác giả xem được bản Draft vừa tạo qua GET /recipes/{slug}")]
    public async Task Create_ThenOwnerReadsDetail_Returns200()
    {
        var author = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var created = await _api.CreateRecipeAsync(author, category.Id);

        var detail = await _api.SendAsync(HttpMethod.Get, $"{Url}/{created.Slug}", author.Token);

        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await detail.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!
            .RowVersion.Should().Be(created.RowVersion);
    }

    [Fact(DisplayName = "FR-RCP-003/D10: title trùng → slug auto-suffix -2, -3 (không bao giờ 409)")]
    public async Task Create_DuplicateTitle_AppendsSuffix()
    {
        var author = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var title = "Bánh xèo " + Guid.NewGuid().ToString("N")[..6];

        var slugs = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var response = await _api.SendAsync(HttpMethod.Post, Url, author.Token, RecipeCommandClient.CreateBody(category.Id, title));
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            slugs.Add((await response.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!.Slug);
        }

        slugs[1].Should().Be(slugs[0] + "-2");
        slugs[2].Should().Be(slugs[0] + "-3");
    }

    [Fact(DisplayName = "FR-RCP-003/D1/D10: slug của recipe đã xóa mềm không được tái sử dụng")]
    public async Task Create_SlugOfSoftDeletedRecipe_IsNotReused()
    {
        var author = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var title = "Chè bưởi " + Guid.NewGuid().ToString("N")[..6];
        var first = await (await _api.SendAsync(HttpMethod.Post, Url, author.Token, RecipeCommandClient.CreateBody(category.Id, title)))
            .Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
            var recipe = await db.Recipes.SingleAsync(r => r.Id == first!.Id);
            recipe.SoftDelete();
            await db.SaveChangesAsync();
        }

        var second = await _api.SendAsync(HttpMethod.Post, Url, author.Token, RecipeCommandClient.CreateBody(category.Id, title));

        second.StatusCode.Should().Be(HttpStatusCode.Created);
        (await second.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!
            .Slug.Should().Be(first!.Slug + "-2");
    }

    [Fact(DisplayName = "FR-RCP-003/D10: title \"Search\" → slug search-1 (không đụng route /recipes/search)")]
    public async Task Create_ReservedSlug_GetsSuffix()
    {
        var author = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();

        var response = await _api.SendAsync(HttpMethod.Post, Url, author.Token, RecipeCommandClient.CreateBody(category.Id, "Search"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!
            .Slug.Should().MatchRegex("^search-1(-[0-9]+)?$");
    }

    [Fact(DisplayName = "FR-RCP-003: Admin tạo được recipe → 201")]
    public async Task Create_AsAdmin_Returns201()
    {
        var admin = await _api.RegisterAdminAsync();
        var category = await _api.CreateCategoryAsync();

        var response = await _api.SendAsync(HttpMethod.Post, Url, admin.Token, RecipeCommandClient.CreateBody(category.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "FR-RCP-003/A1: chưa đăng nhập → 401")]
    public async Task Create_Anonymous_Returns401()
    {
        var category = await _api.CreateCategoryAsync();

        var response = await _api.SendAsync(HttpMethod.Post, Url, token: null, RecipeCommandClient.CreateBody(category.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "FR-RCP-003/D19: cookTime = 0 (món không cần nấu) → 201")]
    public async Task Create_ZeroCookTime_Returns201()
    {
        var author = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var body = new
        {
            title = "Salad dầu giấm",
            description = "Không cần nấu",
            categoryId = category.Id,
            prepTime = 10,
            cookTime = 0,
            servings = 2,
            difficulty = 1,
        };

        var response = await _api.SendAsync(HttpMethod.Post, Url, author.Token, body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await response.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!;
        dto.CookTime.Should().Be(0);
        dto.Difficulty.Should().Be((short)RecipeDifficulty.Easy, "D69 — difficulty nhận cả số 1–4");
        dto.Steps.Should().BeEmpty();
    }

    [Fact(DisplayName = "FR-RCP-003/D4: dữ liệu sai (title ngắn, prepTime 0) → 400 VALIDATION_ERROR kèm lỗi từng trường")]
    public async Task Create_InvalidBody_Returns400()
    {
        var author = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var body = new
        {
            title = "Gỏi",
            description = "Mô tả",
            categoryId = category.Id,
            prepTime = 0,
            cookTime = 5,
            servings = 2,
            difficulty = "Easy",
        };

        var response = await _api.SendAsync(HttpMethod.Post, Url, author.Token, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(RecipeCommandClient.JsonOptions))!;
        problem.Type.Should().Be(ErrorCodes.ValidationError);
        problem.Errors.Keys.Should().Contain(["Title", "PrepTime"]);
    }

    [Fact(DisplayName = "FR-RCP-003/A2 + D4/D69: categoryId không tồn tại → 400 VALIDATION_ERROR (không 422)")]
    public async Task Create_UnknownCategory_Returns400()
    {
        var author = await _api.RegisterAuthorAsync();

        var response = await _api.SendAsync(HttpMethod.Post, Url, author.Token, RecipeCommandClient.CreateBody(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(RecipeCommandClient.JsonOptions))!;
        problem.Type.Should().Be(ErrorCodes.ValidationError);
        problem.Errors.Keys.Should().Contain("CategoryId");
    }

    [Fact(DisplayName = "FR-RCP-003/D8: tạo recipe xóa cache tag \"recipes\" — danh sách của tác giả thấy ngay bản mới")]
    public async Task Create_InvalidatesRecipeListCache()
    {
        var author = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var listUrl = $"{Url}?categoryId={category.Id}";

        var before = await _api.SendAsync(HttpMethod.Get, listUrl, author.Token);
        before.StatusCode.Should().Be(HttpStatusCode.OK);
        (await before.Content.ReadAsStringAsync()).Should().Contain("\"totalCount\":0");

        await _api.CreateRecipeAsync(author, category.Id);

        var after = await _api.SendAsync(HttpMethod.Get, listUrl, author.Token);
        (await after.Content.ReadAsStringAsync()).Should().Contain("\"totalCount\":1");
    }

    [Fact(DisplayName = "D72: recipe tạo xong có slug duy nhất trong DB kể cả khi title có ký tự đặc biệt")]
    public async Task Create_TitleWithSymbols_ProducesCleanSlug()
    {
        var author = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();

        var response = await _api.SendAsync(HttpMethod.Post, Url, author.Token,
            RecipeCommandClient.CreateBody(category.Id, "Bún  -  chả Hà Nội!!"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!
            .Slug.Should().MatchRegex("^bun-cha-ha-noi(-[0-9]+)?$");
    }
}
