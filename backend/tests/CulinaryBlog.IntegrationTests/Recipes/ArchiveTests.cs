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
/// FR-RCP-006 — PATCH /api/v1/recipes/{id}/archive. D1 (không xóa gì), D8 (xóa cache), D71 (rowVersion/ETag),
/// D77 (thoát Archived bằng publish/unpublish), D79 (Draft/Published → Archived, idempotent, If-Match tùy chọn).
/// </summary>
public sealed class ArchiveTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private readonly RecipeCommandClient _api = new(factory);

    private static string ArchiveUrl(Guid id) => $"/api/v1/recipes/{id}/archive";

    private static string PublishUrl(Guid id) => $"/api/v1/recipes/{id}/publish";

    private static async Task<RecipeDetailDto> ReadDetailAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!;

    private static async Task<string?> ReadErrorTypeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!.Type;

    private async Task<Recipe> RecipeInDbAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        return await db.Recipes.AsNoTracking().IgnoreQueryFilters()
            .Include(r => r.Steps).Include(r => r.Ingredients)
            .SingleAsync(r => r.Id == id);
    }

    private async Task EditInDbAsync(Guid id, Action<Recipe> edit)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        edit(await db.Recipes.SingleAsync(r => r.Id == id));
        await db.SaveChangesAsync();
    }

    [Fact(DisplayName = "FR-RCP-006/D79: archive Draft → 200, Status Archived, rowVersion + ETag mới, step/ingredient còn nguyên (D1)")]
    public async Task Archive_Draft_Returns200()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Patch, ArchiveUrl(recipe.Id), owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadDetailAsync(response);
        dto.Status.Should().Be((short)RecipeStatus.Archived);
        dto.RowVersion.Should().NotBe(recipe.RowVersion, "đổi Status làm đổi RowVersion");
        response.Headers.ETag!.Tag.Should().Be($"\"{dto.RowVersion}\"");
        var inDb = await RecipeInDbAsync(recipe.Id);
        inDb.Status.Should().Be(RecipeStatus.Archived);
        inDb.IsDeleted.Should().BeFalse();
        inDb.Steps.Should().HaveCount(2);
        inDb.Ingredients.Should().HaveCount(2);
    }

    [Fact(DisplayName = "FR-RCP-006/D8/D79: archive recipe Published → biến khỏi danh sách, tìm kiếm; Guest xem chi tiết → 403; PublishedAt giữ nguyên")]
    public async Task Archive_Published_HidesFromPublic()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var keyword = "arctest" + Guid.NewGuid().ToString("N")[..10];
        var recipe = await ReadDetailAsync(await _api.SendAsync(HttpMethod.Post, "/api/v1/recipes", owner.Token,
            RecipeCommandClient.CreateBody(category.Id, title: $"Canh {keyword} chua")));
        var published = await ReadDetailAsync(await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token));
        var detailUrl = $"/api/v1/recipes/{recipe.Slug}";
        var listUrl = $"/api/v1/recipes?categoryId={category.Id}";
        var searchUrl = $"/api/v1/recipes/search?q={keyword}";

        async Task<int> CountAsync(string url) =>
            (await (await _api.SendAsync(HttpMethod.Get, url, token: null))
                .Content.ReadFromJsonAsync<PagedResult<RecipeSummaryDto>>(RecipeCommandClient.JsonOptions))!.Items.Count;

        // Đọc trước để cache (D8) có dữ liệu — archive phải xóa được.
        (await _api.SendAsync(HttpMethod.Get, detailUrl, token: null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await CountAsync(listUrl)).Should().Be(1);
        (await CountAsync(searchUrl)).Should().Be(1);

        var response = await _api.SendAsync(HttpMethod.Patch, ArchiveUrl(recipe.Id), owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadDetailAsync(response);
        dto.Status.Should().Be((short)RecipeStatus.Archived);
        dto.PublishedAt.Should().BeCloseTo(published.PublishedAt!.Value, TimeSpan.FromMilliseconds(1), "PostgreSQL lưu tới micro giây");
        (await CountAsync(listUrl)).Should().Be(0);
        (await CountAsync(searchUrl)).Should().Be(0);
        (await _api.SendAsync(HttpMethod.Get, detailUrl, token: null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Get, detailUrl, owner.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "FR-RCP-006/D79: archive recipe đã Archived → 200 idempotent, rowVersion không đổi")]
    public async Task Archive_AlreadyArchived_IsIdempotent()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        var first = await ReadDetailAsync(await _api.SendAsync(HttpMethod.Patch, ArchiveUrl(recipe.Id), owner.Token));

        var response = await _api.SendAsync(HttpMethod.Patch, ArchiveUrl(recipe.Id), owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await ReadDetailAsync(response);
        second.Status.Should().Be((short)RecipeStatus.Archived);
        second.RowVersion.Should().Be(first.RowVersion);
    }

    [Fact(DisplayName = "FR-RCP-006/D77: recipe Archived publish lại → Published, giữ PublishedAt lần đầu")]
    public async Task Archived_Republish_KeepsFirstPublishedAt()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        var first = await ReadDetailAsync(await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token));
        (await _api.SendAsync(HttpMethod.Patch, ArchiveUrl(recipe.Id), owner.Token)).EnsureSuccessStatusCode();

        var again = await ReadDetailAsync(await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token));

        again.Status.Should().Be((short)RecipeStatus.Published);
        again.PublishedAt.Should().BeCloseTo(first.PublishedAt!.Value, TimeSpan.FromMilliseconds(1), "PostgreSQL lưu tới micro giây");
    }

    [Fact(DisplayName = "FR-RCP-006/D71/D79: If-Match đúng rowVersion → 200")]
    public async Task Archive_WithMatchingIfMatch_Returns200()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Patch, ArchiveUrl(recipe.Id), owner.Token, ifMatch: $"\"{recipe.RowVersion}\"");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "FR-RCP-006/D4/D79: If-Match là rowVersion cũ → 409 RECIPE_CONCURRENCY_CONFLICT, vẫn Draft")]
    public async Task Archive_WithStaleIfMatch_Returns409()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        (await _api.SendAsync(HttpMethod.Patch, PublishUrl(recipe.Id), owner.Token)).EnsureSuccessStatusCode();

        var response = await _api.SendAsync(HttpMethod.Patch, ArchiveUrl(recipe.Id), owner.Token, ifMatch: $"\"{recipe.RowVersion}\"");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadErrorTypeAsync(response)).Should().Be(ErrorCodes.RecipeConcurrencyConflict);
        (await RecipeInDbAsync(recipe.Id)).Status.Should().Be(RecipeStatus.Published);
    }

    [Fact(DisplayName = "FR-RCP-006/D79: If-Match không phải base64 → 400 VALIDATION_ERROR")]
    public async Task Archive_WithMalformedIfMatch_Returns400()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Patch, ArchiveUrl(recipe.Id), owner.Token, ifMatch: "\"khong-phai-base64!\"");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorTypeAsync(response)).Should().Be(ErrorCodes.ValidationError);
    }

    [Fact(DisplayName = "FR-RCP-006/A2/NFR-SEC-006: Author khác archive → 403 RECIPE_FORBIDDEN, trạng thái không đổi")]
    public async Task OtherAuthor_Returns403()
    {
        var owner = await _api.RegisterAuthorAsync();
        var other = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Patch, ArchiveUrl(recipe.Id), other.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadErrorTypeAsync(response)).Should().Be(ErrorCodes.RecipeForbidden);
        (await RecipeInDbAsync(recipe.Id)).Status.Should().Be(RecipeStatus.Draft);
    }

    [Fact(DisplayName = "FR-RCP-006: Admin archive recipe của Author khác → 200")]
    public async Task Archive_AsAdmin_Returns200()
    {
        var owner = await _api.RegisterAuthorAsync();
        var admin = await _api.RegisterAdminAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Patch, ArchiveUrl(recipe.Id), admin.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadDetailAsync(response);
        dto.Status.Should().Be((short)RecipeStatus.Archived);
        dto.Author.Id.Should().Be(owner.UserId);
    }

    [Fact(DisplayName = "FR-RCP-006/A1: recipe không tồn tại → 404 RECIPE_NOT_FOUND")]
    public async Task Archive_UnknownRecipe_Returns404()
    {
        var owner = await _api.RegisterAuthorAsync();

        var response = await _api.SendAsync(HttpMethod.Patch, ArchiveUrl(Guid.NewGuid()), owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadErrorTypeAsync(response)).Should().Be(ErrorCodes.RecipeNotFound);
    }

    [Fact(DisplayName = "FR-RCP-006/D1/D78: recipe đã xóa mềm → 404")]
    public async Task Archive_SoftDeletedRecipe_Returns404()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        await EditInDbAsync(recipe.Id, r => r.SoftDelete());

        var response = await _api.SendAsync(HttpMethod.Patch, ArchiveUrl(recipe.Id), owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "FR-RCP-006: chưa đăng nhập → 401")]
    public async Task Anonymous_Returns401()
    {
        var response = await _api.SendAsync(HttpMethod.Patch, ArchiveUrl(Guid.NewGuid()), token: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
