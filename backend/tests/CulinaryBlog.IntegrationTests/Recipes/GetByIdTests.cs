using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Recipes.DTOs;
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
/// D75 — GET /api/v1/recipes/{id:guid}: trang sửa (FR-RCP-004 FE) đọc recipe theo id kèm rowVersion mới nhất.
/// Quyền xem giống FR-RCP-002 (Draft/Archived chỉ owner hoặc Admin).
/// </summary>
public sealed class GetByIdTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private readonly RecipeCommandClient _api = new(factory);

    [Fact(DisplayName = "D75: owner đọc Draft của mình theo id → 200 kèm rowVersion, steps, ingredients")]
    public async Task Owner_Draft_Returns200()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var created = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/{created.Id}", owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!;
        dto.Id.Should().Be(created.Id);
        dto.RowVersion.Should().Be(created.RowVersion);
        dto.Category.Name.Should().Be(category.Name);
        dto.Steps.Should().HaveCount(2);
        dto.Ingredients.Should().HaveCount(2);
    }

    [Fact(DisplayName = "D75: luôn đọc từ DB (không cache) — sau PUT thấy ngay rowVersion mới")]
    public async Task AfterUpdate_ReturnsFreshRowVersion()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var created = await _api.CreateRecipeAsync(owner, category.Id);
        (await _api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/{created.Id}", owner.Token)).EnsureSuccessStatusCode();

        var put = await _api.SendAsync(HttpMethod.Put, $"/api/v1/recipes/{created.Id}", owner.Token,
            new { title = "Tiêu đề mới hoàn toàn", description = "Mô tả", categoryId = category.Id, prepTime = 5, cookTime = 0, servings = 1, difficulty = "Easy" },
            ifMatch: $"\"{created.RowVersion}\"");
        var updated = (await put.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!;

        var again = (await (await _api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/{created.Id}", owner.Token))
            .Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!;
        again.RowVersion.Should().Be(updated.RowVersion);
        again.Title.Should().Be("Tiêu đề mới hoàn toàn");
    }

    [Fact(DisplayName = "D75/NFR-SEC-006: Author khác đọc Draft theo id → 403 RECIPE_FORBIDDEN")]
    public async Task OtherAuthor_Draft_Returns403()
    {
        var owner = await _api.RegisterAuthorAsync();
        var other = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var created = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/{created.Id}", other.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
            .Type.Should().Be(ErrorCodes.RecipeForbidden);
    }

    [Fact(DisplayName = "D75: Admin đọc Draft của người khác → 200")]
    public async Task Admin_Draft_Returns200()
    {
        var owner = await _api.RegisterAuthorAsync();
        var admin = await _api.RegisterAdminAsync();
        var category = await _api.CreateCategoryAsync();
        var created = await _api.CreateRecipeAsync(owner, category.Id);

        (await _api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/{created.Id}", admin.Token))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "D75: Guest đọc recipe Published theo id → 200; Draft → 403")]
    public async Task Guest_PublishedOk_DraftForbidden()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var draft = await _api.CreateRecipeAsync(owner, category.Id);
        var published = await _api.CreateRecipeAsync(owner, category.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
            var entity = await db.Recipes.Include(r => r.Steps).Include(r => r.Ingredients).SingleAsync(r => r.Id == published.Id);
            entity.Publish();
            await db.SaveChangesAsync();
        }

        (await _api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/{published.Id}", token: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/{draft.Id}", token: null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "D75: id không tồn tại → 404 RECIPE_NOT_FOUND; route slug vẫn hoạt động song song")]
    public async Task UnknownId_Returns404_SlugRouteStillWorks()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var created = await _api.CreateRecipeAsync(owner, category.Id);

        var missing = await _api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/{Guid.NewGuid()}", owner.Token);
        var bySlug = await _api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/{created.Slug}", owner.Token);

        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await missing.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
            .Type.Should().Be(ErrorCodes.RecipeNotFound);
        bySlug.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "D75/D72: title có dạng GUID → slug thêm -1, không bị route {id:guid} nuốt mất")]
    public async Task GuidLikeTitle_SlugGetsSuffix()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var guidTitle = Guid.NewGuid().ToString();

        var response = await _api.SendAsync(HttpMethod.Post, "/api/v1/recipes", owner.Token,
            RecipeCommandClient.CreateBody(category.Id, guidTitle));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await response.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!;
        dto.Slug.Should().Be(guidTitle + "-1");
        (await _api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/{dto.Slug}", owner.Token))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
