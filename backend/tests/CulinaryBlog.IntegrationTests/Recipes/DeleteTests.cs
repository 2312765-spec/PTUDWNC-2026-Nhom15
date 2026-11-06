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
/// FR-RCP-007 — DELETE /api/v1/recipes/{id}. D1 (soft delete, không cascade, không xóa file, slug không tái dùng),
/// D2 (category hết recipe thì xóa được), D8 (xóa cache), D78 (404 sau khi xóa, If-Match tùy chọn, khóa dòng).
/// </summary>
public sealed class DeleteTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private readonly RecipeCommandClient _api = new(factory);

    private static string RecipeUrl(Guid id) => $"/api/v1/recipes/{id}";

    private static async Task<string?> ReadErrorTypeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!.Type;

    private async Task<T> WithDbAsync<T>(Func<CulinaryBlogDbContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>());
    }

    private async Task PublishAsync(RecipeCommandClient.TestUser owner, Guid id) =>
        (await _api.SendAsync(HttpMethod.Patch, $"{RecipeUrl(id)}/publish", owner.Token)).EnsureSuccessStatusCode();

    [Fact(DisplayName = "FR-RCP-007/D1: owner xóa → 204, IsDeleted = true, dòng recipe vẫn còn trong DB")]
    public async Task Delete_Owner_SoftDeletes()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Delete, RecipeUrl(recipe.Id), owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var row = await WithDbAsync(db => db.Recipes.IgnoreQueryFilters().AsNoTracking().SingleAsync(r => r.Id == recipe.Id));
        row.IsDeleted.Should().BeTrue();
        row.UpdatedAt.Should().NotBeNull();
        row.Slug.Should().Be(recipe.Slug);
    }

    [Fact(DisplayName = "FR-RCP-007/D1: không cascade — bước, nguyên liệu, ảnh vẫn nguyên trong DB, không bị đánh dấu xóa")]
    public async Task Delete_DoesNotCascade()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        await WithDbAsync(async db =>
        {
            var entity = await db.Recipes.SingleAsync(r => r.Id == recipe.Id);
            var image = entity.AttachImage("https://cdn.example.com/recipes/a.webp", "Ảnh");
            db.RecipeImages.Add(image);
            return await db.SaveChangesAsync();
        });
        var stepVersionsBefore = await WithDbAsync(db => db.RecipeSteps.Where(s => s.RecipeId == recipe.Id)
            .OrderBy(s => s.StepNumber).Select(s => s.RowVersion).ToListAsync());

        (await _api.SendAsync(HttpMethod.Delete, RecipeUrl(recipe.Id), owner.Token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (steps, ingredients, images) = await WithDbAsync(async db => (
            await db.RecipeSteps.IgnoreQueryFilters().Where(s => s.RecipeId == recipe.Id).OrderBy(s => s.StepNumber).ToListAsync(),
            await db.RecipeIngredients.IgnoreQueryFilters().Where(i => i.RecipeId == recipe.Id).ToListAsync(),
            await db.RecipeImages.IgnoreQueryFilters().Where(i => i.RecipeId == recipe.Id).ToListAsync()));
        steps.Should().HaveCount(2).And.OnlyContain(s => !s.IsDeleted);
        steps.Select(s => s.RowVersion).Should().BeEquivalentTo(stepVersionsBefore, o => o.WithStrictOrdering(),
            "D78 — xóa mềm recipe không ghi lại dòng con");
        ingredients.Should().HaveCount(2).And.OnlyContain(i => !i.IsDeleted);
        images.Should().ContainSingle().Which.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "FR-RCP-007/D8/D78: recipe Published bị xóa → GET theo slug và theo id trả 404 (cache đã xóa), biến mất khỏi danh sách")]
    public async Task Delete_Published_DisappearsEverywhere()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        await PublishAsync(owner, recipe.Id);
        var detailUrl = $"/api/v1/recipes/{recipe.Slug}";
        var listUrl = $"/api/v1/recipes?categoryId={category.Id}";
        (await _api.SendAsync(HttpMethod.Get, detailUrl, token: null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _api.SendAsync(HttpMethod.Get, listUrl, token: null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await _api.SendAsync(HttpMethod.Delete, RecipeUrl(recipe.Id), owner.Token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await _api.SendAsync(HttpMethod.Get, detailUrl, token: null);
        detail.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadErrorTypeAsync(detail)).Should().Be(ErrorCodes.RecipeNotFound);
        (await _api.SendAsync(HttpMethod.Get, RecipeUrl(recipe.Id), owner.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var list = await (await _api.SendAsync(HttpMethod.Get, listUrl, token: null))
            .Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(RecipeCommandClient.JsonOptions);
        list.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "FR-RCP-007/D78: sau khi xóa, mọi lệnh ghi trên recipe (sửa, publish, bước, nguyên liệu, xóa lại) → 404")]
    public async Task AfterDelete_WriteCommands_Return404()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        (await _api.SendAsync(HttpMethod.Delete, RecipeUrl(recipe.Id), owner.Token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var responses = new[]
        {
            await _api.SendAsync(HttpMethod.Put, RecipeUrl(recipe.Id), owner.Token,
                new { title = "Sửa sau khi xóa", description = "Mô tả", categoryId = category.Id, prepTime = 20, cookTime = 10, servings = 4, difficulty = "Easy" },
                ifMatch: $"\"{recipe.RowVersion}\""),
            await _api.SendAsync(HttpMethod.Patch, $"{RecipeUrl(recipe.Id)}/publish", owner.Token),
            await _api.SendAsync(HttpMethod.Patch, $"{RecipeUrl(recipe.Id)}/unpublish", owner.Token),
            await _api.SendAsync(HttpMethod.Post, $"{RecipeUrl(recipe.Id)}/steps", owner.Token, new { title = "Bước", description = "Mô tả" }),
            await _api.SendAsync(HttpMethod.Delete, $"{RecipeUrl(recipe.Id)}/steps/{recipe.Steps[0].Id}", owner.Token),
            await _api.SendAsync(HttpMethod.Post, $"{RecipeUrl(recipe.Id)}/ingredients", owner.Token, new { name = "Muối" }),
            await _api.SendAsync(HttpMethod.Put, $"{RecipeUrl(recipe.Id)}/ingredients/{recipe.Ingredients[0].Id}", owner.Token, new { name = "Muối" }),
            await _api.SendAsync(HttpMethod.Delete, RecipeUrl(recipe.Id), owner.Token),
        };

        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} {1}", response.RequestMessage!.Method, response.RequestMessage.RequestUri);
            (await ReadErrorTypeAsync(response)).Should().Be(ErrorCodes.RecipeNotFound);
        }
    }

    [Fact(DisplayName = "FR-RCP-007/D1/D10: slug của recipe đã xóa không được tái dùng — tạo lại cùng title → slug -2")]
    public async Task Delete_SlugIsNotReused()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var title = "Canh chua cá lóc " + Guid.NewGuid().ToString("N")[..6];
        var first = await (await _api.SendAsync(HttpMethod.Post, "/api/v1/recipes", owner.Token, RecipeCommandClient.CreateBody(category.Id, title)))
            .Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions);
        (await _api.SendAsync(HttpMethod.Delete, RecipeUrl(first!.Id), owner.Token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var second = await _api.SendAsync(HttpMethod.Post, "/api/v1/recipes", owner.Token, RecipeCommandClient.CreateBody(category.Id, title));

        second.StatusCode.Should().Be(HttpStatusCode.Created);
        (await second.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!.Slug.Should().Be(first.Slug + "-2");
    }

    [Fact(DisplayName = "FR-RCP-007/D2: xóa recipe cuối cùng của category → Admin xóa được category (recipe xóa mềm không tính)")]
    public async Task Delete_LastRecipe_AllowsCategoryDelete()
    {
        var owner = await _api.RegisterAuthorAsync();
        var admin = await _api.RegisterAdminAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/categories/{category.Id}", admin.Token))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "category còn recipe");

        (await _api.SendAsync(HttpMethod.Delete, RecipeUrl(recipe.Id), owner.Token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/categories/{category.Id}", admin.Token))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "FR-RCP-007/A2: Author khác xóa → 403 RECIPE_FORBIDDEN, recipe còn nguyên")]
    public async Task Delete_OtherAuthor_Returns403()
    {
        var owner = await _api.RegisterAuthorAsync();
        var other = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Delete, RecipeUrl(recipe.Id), other.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadErrorTypeAsync(response)).Should().Be(ErrorCodes.RecipeForbidden);
        (await WithDbAsync(db => db.Recipes.AnyAsync(r => r.Id == recipe.Id))).Should().BeTrue();
    }

    [Fact(DisplayName = "FR-RCP-007: Admin xóa recipe của Author khác → 204")]
    public async Task Delete_AsAdmin_Returns204()
    {
        var owner = await _api.RegisterAuthorAsync();
        var admin = await _api.RegisterAdminAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Delete, RecipeUrl(recipe.Id), admin.Token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "FR-RCP-007/A1: recipe không tồn tại → 404 RECIPE_NOT_FOUND")]
    public async Task Delete_Unknown_Returns404()
    {
        var owner = await _api.RegisterAuthorAsync();

        var response = await _api.SendAsync(HttpMethod.Delete, RecipeUrl(Guid.NewGuid()), owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadErrorTypeAsync(response)).Should().Be(ErrorCodes.RecipeNotFound);
    }

    [Fact(DisplayName = "FR-RCP-007/D4/D78: If-Match là rowVersion cũ → 409 RECIPE_CONCURRENCY_CONFLICT, không xóa")]
    public async Task Delete_WithStaleIfMatch_Returns409()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        await PublishAsync(owner, recipe.Id);

        var response = await _api.SendAsync(HttpMethod.Delete, RecipeUrl(recipe.Id), owner.Token, ifMatch: $"\"{recipe.RowVersion}\"");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadErrorTypeAsync(response)).Should().Be(ErrorCodes.RecipeConcurrencyConflict);
        (await WithDbAsync(db => db.Recipes.AnyAsync(r => r.Id == recipe.Id))).Should().BeTrue();
    }

    [Fact(DisplayName = "FR-RCP-007/D78: If-Match đúng rowVersion → 204; sai định dạng → 400 VALIDATION_ERROR")]
    public async Task Delete_IfMatchFormat()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var malformed = await _api.SendAsync(HttpMethod.Delete, RecipeUrl(recipe.Id), owner.Token, ifMatch: "\"%%%\"");
        var matching = await _api.SendAsync(HttpMethod.Delete, RecipeUrl(recipe.Id), owner.Token, ifMatch: $"\"{recipe.RowVersion}\"");

        malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorTypeAsync(malformed)).Should().Be(ErrorCodes.ValidationError);
        matching.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "FR-RCP-007: chưa đăng nhập → 401")]
    public async Task Delete_Anonymous_Returns401()
    {
        var response = await _api.SendAsync(HttpMethod.Delete, RecipeUrl(Guid.NewGuid()), token: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
