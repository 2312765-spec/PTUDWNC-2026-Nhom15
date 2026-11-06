using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Models;
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

/// <summary>
/// FR-RCP-005 — PATCH /api/v1/recipes/{id}/publish | /unpublish. D3 (≥ 1 step VÀ ≥ 1 ingredient), D4 (400 thay 422),
/// D8 (xóa cache), D71 (rowVersion/ETag), D77 (chuyển trạng thái, idempotent, If-Match tùy chọn, PublishedAt).
/// </summary>
public sealed class PublishTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private readonly RecipeCommandClient _api = new(factory);

    private static string PublishUrl(Guid id) => $"/api/v1/recipes/{id}/publish";

    private static string UnpublishUrl(Guid id) => $"/api/v1/recipes/{id}/unpublish";

    private static async Task<RecipeDetailDto> ReadDetailAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!;

    private static async Task<string?> ReadErrorTypeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!.Type;

    private async Task<Recipe> RecipeInDbAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        return await db.Recipes.AsNoTracking().IgnoreQueryFilters().SingleAsync(r => r.Id == id);
    }

    private async Task EditInDbAsync(Guid id, Action<Recipe> edit)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        edit(await db.Recipes.SingleAsync(r => r.Id == id));
        await db.SaveChangesAsync();
    }

    [Fact(DisplayName = "FR-RCP-005/D71: publish Draft đủ step + ingredient → 200, Status Published, PublishedAt, rowVersion + ETag mới")]
    public async Task Publish_Draft_Returns200()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadDetailAsync(response);
        dto.Status.Should().Be((short)RecipeStatus.Published);
        dto.PublishedAt.Should().NotBeNull();
        dto.Steps.Should().HaveCount(2);
        dto.Ingredients.Should().HaveCount(2);
        dto.Category.Name.Should().Be(category.Name);
        dto.RowVersion.Should().NotBe(recipe.RowVersion, "đổi Status làm đổi RowVersion");
        response.Headers.ETag!.Tag.Should().Be($"\"{dto.RowVersion}\"");
        (await RecipeInDbAsync(recipe.Id)).Status.Should().Be(RecipeStatus.Published);
    }

    [Fact(DisplayName = "FR-RCP-005/D8: publish xóa cache — Guest trước đó bị 403, sau publish xem được chi tiết và thấy trong danh sách")]
    public async Task Publish_MakesRecipePublic()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        var detailUrl = $"/api/v1/recipes/{recipe.Slug}";
        var listUrl = $"/api/v1/recipes?categoryId={category.Id}";
        (await _api.SendAsync(HttpMethod.Get, detailUrl, owner.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _api.SendAsync(HttpMethod.Get, detailUrl, token: null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await (await _api.SendAsync(HttpMethod.Get, listUrl, token: null))
            .Content.ReadFromJsonAsync<PagedResult<RecipeSummaryDto>>(RecipeCommandClient.JsonOptions))!.Items.Should().BeEmpty();

        (await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token)).StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await _api.SendAsync(HttpMethod.Get, detailUrl, token: null);
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDetailAsync(detail)).Status.Should().Be((short)RecipeStatus.Published);
        (await (await _api.SendAsync(HttpMethod.Get, listUrl, token: null))
            .Content.ReadFromJsonAsync<PagedResult<RecipeSummaryDto>>(RecipeCommandClient.JsonOptions))!
            .Items.Select(r => r.Id).Should().Equal(recipe.Id);
    }

    [Fact(DisplayName = "FR-RCP-005: publish đưa recipe vào kết quả tìm kiếm, unpublish đưa ra khỏi")]
    public async Task Publish_AppearsInSearch()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var keyword = "pubtest" + Guid.NewGuid().ToString("N")[..10];
        var created = await _api.SendAsync(HttpMethod.Post, "/api/v1/recipes", owner.Token,
            RecipeCommandClient.CreateBody(category.Id, title: $"Bánh {keyword} nướng"));
        var recipe = await ReadDetailAsync(created);
        var searchUrl = $"/api/v1/recipes/search?q={keyword}";

        async Task<int> SearchCountAsync() =>
            (await (await _api.SendAsync(HttpMethod.Get, searchUrl, token: null))
                .Content.ReadFromJsonAsync<PagedResult<RecipeSummaryDto>>(RecipeCommandClient.JsonOptions))!.Items.Count;

        (await SearchCountAsync()).Should().Be(0);
        (await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token)).EnsureSuccessStatusCode();
        (await SearchCountAsync()).Should().Be(1);
        (await _api.SendAsync(HttpMethod.Patch, UnpublishUrl(recipe.Id), owner.Token)).EnsureSuccessStatusCode();
        (await SearchCountAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "FR-RCP-005/A1/D3/D4: publish recipe chưa có bước → 400 RECIPE_PUBLISH_INCOMPLETE (không phải 422), vẫn Draft")]
    public async Task Publish_WithoutSteps_Returns400()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id, steps: Array.Empty<object>());

        var response = await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorTypeAsync(response)).Should().Be(ErrorCodes.RecipePublishIncomplete);
        (await RecipeInDbAsync(recipe.Id)).Status.Should().Be(RecipeStatus.Draft);
    }

    [Fact(DisplayName = "FR-RCP-005/D3: publish recipe chưa có nguyên liệu → 400 RECIPE_PUBLISH_INCOMPLETE")]
    public async Task Publish_WithoutIngredients_Returns400()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id, ingredients: Array.Empty<object>());

        var response = await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorTypeAsync(response)).Should().Be(ErrorCodes.RecipePublishIncomplete);
        (await RecipeInDbAsync(recipe.Id)).PublishedAt.Should().BeNull();
    }

    [Fact(DisplayName = "FR-RCP-005/A2/D77: publish recipe đã Published → 200 idempotent, rowVersion và PublishedAt không đổi")]
    public async Task Publish_AlreadyPublished_IsIdempotent()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        var first = await ReadDetailAsync(await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token));

        var response = await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await ReadDetailAsync(response);
        second.Status.Should().Be((short)RecipeStatus.Published);
        second.RowVersion.Should().Be(first.RowVersion);
        second.PublishedAt.Should().BeCloseTo(first.PublishedAt!.Value, TimeSpan.FromMilliseconds(1), "PostgreSQL lưu tới micro giây");
    }

    [Fact(DisplayName = "FR-RCP-005: unpublish Published → 200 Draft, Guest không xem được nữa (403)")]
    public async Task Unpublish_Published_Returns200()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        (await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token)).EnsureSuccessStatusCode();
        var detailUrl = $"/api/v1/recipes/{recipe.Slug}";
        (await _api.SendAsync(HttpMethod.Get, detailUrl, token: null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _api.SendAsync(HttpMethod.Patch, UnpublishUrl(recipe.Id), owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDetailAsync(response)).Status.Should().Be((short)RecipeStatus.Draft);
        response.Headers.ETag.Should().NotBeNull();
        (await _api.SendAsync(HttpMethod.Get, detailUrl, token: null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "FR-RCP-005/A2: unpublish recipe đang Draft → 200 idempotent")]
    public async Task Unpublish_Draft_IsIdempotent()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Patch, UnpublishUrl(recipe.Id), owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadDetailAsync(response);
        dto.Status.Should().Be((short)RecipeStatus.Draft);
        dto.RowVersion.Should().Be(recipe.RowVersion);
    }

    [Fact(DisplayName = "FR-RCP-005/D77: publish lại sau unpublish → giữ PublishedAt của lần xuất bản đầu tiên")]
    public async Task Republish_KeepsFirstPublishedAt()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        var first = await ReadDetailAsync(await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token));
        (await _api.SendAsync(HttpMethod.Patch, UnpublishUrl(recipe.Id), owner.Token)).EnsureSuccessStatusCode();

        var again = await ReadDetailAsync(await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token));

        again.Status.Should().Be((short)RecipeStatus.Published);
        again.PublishedAt.Should().BeCloseTo(first.PublishedAt!.Value, TimeSpan.FromMilliseconds(1), "PostgreSQL lưu tới micro giây");
    }

    [Theory(DisplayName = "FR-RCP-005/D77: recipe Archived — publish → Published, unpublish → Draft")]
    [InlineData(true, RecipeStatus.Published)]
    [InlineData(false, RecipeStatus.Draft)]
    public async Task Archived_CanBePublishedOrUnpublished(bool publish, RecipeStatus expected)
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        await EditInDbAsync(recipe.Id, r => r.Archive());

        var response = await _api.SendAsync(HttpMethod.Patch, publish ? PublishUrl(recipe.Id) : UnpublishUrl(recipe.Id), owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDetailAsync(response)).Status.Should().Be((short)expected);
    }

    [Fact(DisplayName = "FR-RCP-005/D71/D77: If-Match đúng rowVersion → 200")]
    public async Task Publish_WithMatchingIfMatch_Returns200()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token, ifMatch: $"\"{recipe.RowVersion}\"");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "FR-RCP-005/D4/D77: If-Match là rowVersion cũ → 409 RECIPE_CONCURRENCY_CONFLICT, trạng thái không đổi")]
    public async Task Publish_WithStaleIfMatch_Returns409()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        (await _api.SendAsync(HttpMethod.Put, $"/api/v1/recipes/{recipe.Id}", owner.Token,
            new { title = "Gỏi cuốn đã sửa", description = "Mô tả", categoryId = category.Id, prepTime = 20, cookTime = 10, servings = 4, difficulty = "Easy" },
            ifMatch: $"\"{recipe.RowVersion}\"")).EnsureSuccessStatusCode();

        var response = await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token, ifMatch: $"\"{recipe.RowVersion}\"");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadErrorTypeAsync(response)).Should().Be(ErrorCodes.RecipeConcurrencyConflict);
        (await RecipeInDbAsync(recipe.Id)).Status.Should().Be(RecipeStatus.Draft);
    }

    [Fact(DisplayName = "FR-RCP-005/D77: If-Match không phải base64 → 400 VALIDATION_ERROR")]
    public async Task Publish_WithMalformedIfMatch_Returns400()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token, ifMatch: "\"khong-phai-base64!\"");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorTypeAsync(response)).Should().Be(ErrorCodes.ValidationError);
    }

    [Fact(DisplayName = "FR-RCP-005/NFR-SEC-006: Author khác publish / unpublish → 403 RECIPE_FORBIDDEN, trạng thái không đổi")]
    public async Task OtherAuthor_Returns403()
    {
        var owner = await _api.RegisterAuthorAsync();
        var other = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var publish = await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), other.Token);
        var unpublish = await _api.SendAsync(HttpMethod.Patch, UnpublishUrl(recipe.Id), other.Token);

        publish.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadErrorTypeAsync(publish)).Should().Be(ErrorCodes.RecipeForbidden);
        unpublish.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await RecipeInDbAsync(recipe.Id)).Status.Should().Be(RecipeStatus.Draft);
    }

    [Fact(DisplayName = "FR-RCP-005: Admin publish recipe của Author khác → 200")]
    public async Task Publish_AsAdmin_Returns200()
    {
        var owner = await _api.RegisterAuthorAsync();
        var admin = await _api.RegisterAdminAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), admin.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDetailAsync(response)).Author.Id.Should().Be(owner.UserId);
    }

    [Fact(DisplayName = "FR-RCP-005: recipe không tồn tại → 404 RECIPE_NOT_FOUND")]
    public async Task Publish_UnknownRecipe_Returns404()
    {
        var owner = await _api.RegisterAuthorAsync();

        var response = await _api.SendAsync(HttpMethod.Patch, PublishUrl(Guid.NewGuid()), owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadErrorTypeAsync(response)).Should().Be(ErrorCodes.RecipeNotFound);
    }

    [Fact(DisplayName = "FR-RCP-005/D1: recipe đã xóa mềm → 404")]
    public async Task Publish_SoftDeletedRecipe_Returns404()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        await EditInDbAsync(recipe.Id, r => r.SoftDelete());

        var response = await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "FR-RCP-005: chưa đăng nhập → 401")]
    public async Task Anonymous_Returns401()
    {
        var response = await _api.SendAsync(HttpMethod.Patch, PublishUrl(Guid.NewGuid()), token: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
