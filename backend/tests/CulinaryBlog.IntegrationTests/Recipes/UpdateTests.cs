using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Recipes.DTOs;
using CulinaryBlog.IntegrationTests.Common;
using CulinaryBlog.IntegrationTests.Recipes.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Recipes;

/// <summary>FR-RCP-004 — PUT /api/v1/recipes/{id}. A1 403, A2 409 (D4/D34/D71), A3 404, D13, D69, D8.</summary>
public sealed class UpdateTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private readonly RecipeCommandClient _api = new(factory);

    private static object UpdateBody(Guid categoryId, string title = "Gỏi cuốn chay thanh đạm") => new
    {
        title,
        description = "Phiên bản chay, không tôm thịt",
        categoryId,
        prepTime = 25,
        cookTime = 0,
        servings = 2,
        difficulty = "Medium",
        instructions = "Cuốn chặt tay",
    };

    private static string IfMatch(string rowVersion) => $"\"{rowVersion}\"";

    private Task<HttpResponseMessage> PutAsync(Guid id, string? token, object body, string? ifMatch) =>
        _api.SendAsync(HttpMethod.Put, $"/api/v1/recipes/{id}", token, body, ifMatch);

    [Fact(DisplayName = "FR-RCP-004: owner sửa → 200, dữ liệu mới, slug giữ nguyên (D13), RowVersion + ETag mới")]
    public async Task Update_AsOwner_Returns200()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var otherCategory = await _api.CreateCategoryAsync();
        var created = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await PutAsync(created.Id, owner.Token, UpdateBody(otherCategory.Id), IfMatch(created.RowVersion));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!;
        dto.Title.Should().Be("Gỏi cuốn chay thanh đạm");
        dto.Slug.Should().Be(created.Slug, "D13 — slug bất biến kể cả khi đổi title");
        dto.CookTime.Should().Be(0);
        dto.Difficulty.Should().Be(2);
        dto.Instructions.Should().Be("Cuốn chặt tay");
        dto.Category.Id.Should().Be(otherCategory.Id);
        dto.Category.Name.Should().Be(otherCategory.Name);
        dto.Nutrition!.Calories.Should().Be(250m, "D69 — không gửi nutrition thì giữ nguyên");
        dto.Steps.Should().HaveCount(2, "PUT không đụng steps");
        dto.RowVersion.Should().NotBe(created.RowVersion);
        response.Headers.ETag!.Tag.Should().Be($"\"{dto.RowVersion}\"");
    }

    [Fact(DisplayName = "FR-RCP-004/A2, D4/D71: sửa bằng RowVersion cũ → 409 RECIPE_CONCURRENCY_CONFLICT, dữ liệu không bị ghi đè")]
    public async Task Update_StaleRowVersion_Returns409()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var created = await _api.CreateRecipeAsync(owner, category.Id);
        (await PutAsync(created.Id, owner.Token, UpdateBody(category.Id, "Bản sửa thứ nhất"), IfMatch(created.RowVersion)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var stale = await PutAsync(created.Id, owner.Token, UpdateBody(category.Id, "Bản sửa trên dữ liệu cũ"), IfMatch(created.RowVersion));

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
            .Type.Should().Be(ErrorCodes.RecipeConcurrencyConflict);
        var detail = await _api.SendAsync(HttpMethod.Get, $"/api/v1/recipes/{created.Slug}", owner.Token);
        (await detail.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!
            .Title.Should().Be("Bản sửa thứ nhất");
    }

    [Fact(DisplayName = "FR-RCP-004/D71: thiếu header If-Match → 400 VALIDATION_ERROR (trường RowVersion)")]
    public async Task Update_MissingIfMatch_Returns400()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var created = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await PutAsync(created.Id, owner.Token, UpdateBody(category.Id), ifMatch: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(RecipeCommandClient.JsonOptions))!;
        problem.Type.Should().Be(ErrorCodes.ValidationError);
        problem.Errors.Keys.Should().Contain("RowVersion");
    }

    [Fact(DisplayName = "FR-RCP-004/A1: Author khác sửa → 403 RECIPE_FORBIDDEN")]
    public async Task Update_OtherAuthor_Returns403()
    {
        var owner = await _api.RegisterAuthorAsync();
        var other = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var created = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await PutAsync(created.Id, other.Token, UpdateBody(category.Id), IfMatch(created.RowVersion));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
            .Type.Should().Be(ErrorCodes.RecipeForbidden);
    }

    [Fact(DisplayName = "FR-RCP-004: Admin sửa recipe của Author → 200")]
    public async Task Update_AsAdmin_Returns200()
    {
        var owner = await _api.RegisterAuthorAsync();
        var admin = await _api.RegisterAdminAsync();
        var category = await _api.CreateCategoryAsync();
        var created = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await PutAsync(created.Id, admin.Token, UpdateBody(category.Id), IfMatch(created.RowVersion));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!
            .Author.Id.Should().Be(owner.UserId, "Admin sửa không đổi tác giả");
    }

    [Fact(DisplayName = "FR-RCP-004/A3: id không tồn tại → 404 RECIPE_NOT_FOUND")]
    public async Task Update_UnknownId_Returns404()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();

        var response = await PutAsync(Guid.NewGuid(), owner.Token, UpdateBody(category.Id), IfMatch("AQIDBA=="));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
            .Type.Should().Be(ErrorCodes.RecipeNotFound);
    }

    [Fact(DisplayName = "FR-RCP-004: chưa đăng nhập → 401")]
    public async Task Update_Anonymous_Returns401()
    {
        var category = await _api.CreateCategoryAsync();

        var response = await PutAsync(Guid.NewGuid(), token: null, UpdateBody(category.Id), IfMatch("AQIDBA=="));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "FR-RCP-004/D4: body sai (title ngắn, category không tồn tại) → 400 VALIDATION_ERROR")]
    public async Task Update_InvalidBody_Returns400()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var created = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await PutAsync(created.Id, owner.Token, UpdateBody(Guid.NewGuid(), "Gỏi"), IfMatch(created.RowVersion));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(RecipeCommandClient.JsonOptions))!;
        problem.Type.Should().Be(ErrorCodes.ValidationError);
        problem.Errors.Keys.Should().Contain(["Title", "CategoryId"]);
    }

    [Fact(DisplayName = "FR-RCP-004/D8: sửa xong xóa cache recipe:{slug} — GET chi tiết thấy ngay bản mới")]
    public async Task Update_InvalidatesDetailCache()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var created = await _api.CreateRecipeAsync(owner, category.Id);
        var detailUrl = $"/api/v1/recipes/{created.Slug}";
        (await _api.SendAsync(HttpMethod.Get, detailUrl, owner.Token)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await PutAsync(created.Id, owner.Token, UpdateBody(category.Id, "Tiêu đề sau khi sửa"), IfMatch(created.RowVersion)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = (await (await _api.SendAsync(HttpMethod.Get, detailUrl, owner.Token))
            .Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!;
        detail.Title.Should().Be("Tiêu đề sau khi sửa");
    }

    [Fact(DisplayName = "FR-RCP-004/D71: dùng RowVersion vừa nhận để sửa tiếp nhiều lần liên tiếp → đều 200")]
    public async Task Update_ChainedWithReturnedRowVersion_Succeeds()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var current = await _api.CreateRecipeAsync(owner, category.Id);

        for (var i = 1; i <= 3; i++)
        {
            var response = await PutAsync(current.Id, owner.Token, UpdateBody(category.Id, $"Lần sửa thứ {i} của món"), IfMatch(current.RowVersion));
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            current = (await response.Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!;
        }

        current.Title.Should().Be("Lần sửa thứ 3 của món");
    }
}
