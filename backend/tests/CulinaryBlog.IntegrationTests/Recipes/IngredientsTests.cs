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
/// FR-RCP-009 — POST/PUT/DELETE /api/v1/recipes/{id}/ingredients/{ingredientId?}. D7 (quantity/unit nullable,
/// orderIndex), D31 (thêm con vào aggregate đã track), D71 (không đổi RowVersion recipe), D76 (vị trí, hard
/// delete, khóa dòng, RECIPE_INGREDIENT_NOT_FOUND).
/// </summary>
public sealed class IngredientsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private readonly RecipeCommandClient _api = new(factory);

    private static object FourIngredients => Enumerable.Range(1, 4)
        .Select(i => new { name = $"NL {i}", quantity = (decimal?)i, unit = (string?)"g", notes = (string?)null })
        .ToArray();

    private static string IngredientsUrl(Guid recipeId) => $"/api/v1/recipes/{recipeId}/ingredients";

    private static object IngredientBody(string name = "Hành lá", decimal? quantity = 2m, string? unit = "cây", int? orderIndex = null) =>
        new { name, quantity, unit, notes = "thái nhỏ", orderIndex };

    private async Task<(int Order, string Name)[]> IngredientsInDbAsync(Guid recipeId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var rows = await db.RecipeIngredients.IgnoreQueryFilters()
            .Where(i => i.RecipeId == recipeId)
            .OrderBy(i => i.OrderIndex)
            .Select(i => new { i.OrderIndex, i.Name })
            .ToListAsync();
        return [.. rows.Select(r => (r.OrderIndex, r.Name))];
    }

    private async Task PublishInDbAsync(Guid recipeId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var entity = await db.Recipes.Include(r => r.Steps).Include(r => r.Ingredients).SingleAsync(r => r.Id == recipeId);
        entity.Publish();
        await db.SaveChangesAsync();
    }

    [Fact(DisplayName = "FR-RCP-009/D7/D31: thêm nguyên liệu không gửi orderIndex → 201, xếp cuối danh sách")]
    public async Task Add_Returns201AtEnd()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Post, IngredientsUrl(recipe.Id), owner.Token, IngredientBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await response.Content.ReadFromJsonAsync<RecipeIngredientDto>(RecipeCommandClient.JsonOptions))!;
        dto.Name.Should().Be("Hành lá");
        dto.Quantity.Should().Be(2m);
        dto.Unit.Should().Be("cây");
        dto.Notes.Should().Be("thái nhỏ");
        dto.OrderIndex.Should().Be(2);
        (await IngredientsInDbAsync(recipe.Id)).Select(i => i.Name).Should().Equal("Tôm", "Muối", "Hành lá");
    }

    [Fact(DisplayName = "FR-RCP-009/D7: nguyên liệu \"vừa đủ\" — quantity và unit null → 201")]
    public async Task Add_NullQuantityAndUnit_Returns201()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Post, IngredientsUrl(recipe.Id), owner.Token,
            new { name = "Tiêu", quantity = (decimal?)null, unit = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await response.Content.ReadFromJsonAsync<RecipeIngredientDto>(RecipeCommandClient.JsonOptions))!;
        dto.Quantity.Should().BeNull();
        dto.Unit.Should().BeNull();
    }

    [Fact(DisplayName = "FR-RCP-009/D76: thêm với orderIndex = 0 → chèn lên đầu, các nguyên liệu khác dồn xuống")]
    public async Task Add_WithOrderIndex_InsertsAtPosition()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Post, IngredientsUrl(recipe.Id), owner.Token, IngredientBody(orderIndex: 0));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await IngredientsInDbAsync(recipe.Id)).Should().Equal((0, "Hành lá"), (1, "Tôm"), (2, "Muối"));
    }

    [Theory(DisplayName = "FR-RCP-009/D4/D7: body sai → 400 VALIDATION_ERROR")]
    [InlineData("", 1, "Name")]
    [InlineData("Đường", 0, "Quantity")]
    [InlineData("Đường", -5, "Quantity")]
    public async Task Add_InvalidBody_Returns400(string name, int quantity, string field)
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Post, IngredientsUrl(recipe.Id), owner.Token,
            new { name, quantity = (decimal)quantity, unit = "g" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(RecipeCommandClient.JsonOptions))!;
        problem.Type.Should().Be(ErrorCodes.ValidationError);
        problem.Errors.Keys.Should().Contain(field);
        (await IngredientsInDbAsync(recipe.Id)).Should().HaveCount(2);
    }

    [Fact(DisplayName = "FR-RCP-009/D76: orderIndex vượt số nguyên liệu → 400 VALIDATION_ERROR")]
    public async Task Add_OrderIndexOutOfRange_Returns400()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Post, IngredientsUrl(recipe.Id), owner.Token, IngredientBody(orderIndex: 3));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
            .Type.Should().Be(ErrorCodes.ValidationError);
    }

    [Fact(DisplayName = "FR-RCP-009: sửa nội dung không gửi orderIndex → 200, giữ vị trí, null = xóa quantity/unit/notes")]
    public async Task Update_ContentOnly_KeepsPosition()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Put, $"{IngredientsUrl(recipe.Id)}/{recipe.Ingredients[0].Id}", owner.Token,
            new { name = "Tôm sú", quantity = (decimal?)null, unit = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.Content.ReadFromJsonAsync<List<RecipeIngredientDto>>(RecipeCommandClient.JsonOptions))!;
        list.Select(i => i.Name).Should().Equal("Tôm sú", "Muối");
        list[0].Quantity.Should().BeNull();
        list[0].Unit.Should().BeNull();
        list[0].Notes.Should().BeNull();
        list[0].Id.Should().Be(recipe.Ingredients[0].Id);
    }

    [Theory(DisplayName = "FR-RCP-009/D76: đổi vị trí (lên đầu, xuống cuối, đổi chỗ 2 nguyên liệu kề nhau) → 200, toàn bộ danh sách theo thứ tự mới")]
    [InlineData(3, 0, new[] { "NL 4 sửa", "NL 1", "NL 2", "NL 3" })]
    [InlineData(0, 3, new[] { "NL 2", "NL 3", "NL 4", "NL 1 sửa" })]
    [InlineData(0, 1, new[] { "NL 2", "NL 1 sửa", "NL 3", "NL 4" })]
    public async Task Update_MovesIngredient(int index, int newOrderIndex, string[] expectedNames)
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id, ingredients: FourIngredients);
        var target = recipe.Ingredients[index];

        var response = await _api.SendAsync(HttpMethod.Put, $"{IngredientsUrl(recipe.Id)}/{target.Id}", owner.Token,
            new { name = target.Name + " sửa", quantity = 1m, unit = "g", orderIndex = newOrderIndex });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.Content.ReadFromJsonAsync<List<RecipeIngredientDto>>(RecipeCommandClient.JsonOptions))!;
        list.Select(i => i.Name).Should().Equal(expectedNames);
        list.Select(i => i.OrderIndex).Should().Equal(0, 1, 2, 3);
        (await IngredientsInDbAsync(recipe.Id)).Select(i => i.Name).Should().Equal(expectedNames);
    }

    [Fact(DisplayName = "FR-RCP-009/D76: PUT orderIndex vượt số nguyên liệu → 400 VALIDATION_ERROR")]
    public async Task Update_OrderIndexOutOfRange_Returns400()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Put, $"{IngredientsUrl(recipe.Id)}/{recipe.Ingredients[0].Id}", owner.Token,
            IngredientBody(orderIndex: 2));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
            .Type.Should().Be(ErrorCodes.ValidationError);
    }

    [Theory(DisplayName = "FR-RCP-009/D76: xóa nguyên liệu đầu / giữa / cuối → 204, hard delete, orderIndex liên tục")]
    [InlineData(0, new[] { "NL 2", "NL 3", "NL 4" })]
    [InlineData(1, new[] { "NL 1", "NL 3", "NL 4" })]
    [InlineData(3, new[] { "NL 1", "NL 2", "NL 3" })]
    public async Task Delete_ReindexesRemaining(int index, string[] expectedNames)
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id, ingredients: FourIngredients);

        var response = await _api.SendAsync(HttpMethod.Delete, $"{IngredientsUrl(recipe.Id)}/{recipe.Ingredients[index].Id}", owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var rows = await IngredientsInDbAsync(recipe.Id);
        rows.Select(i => i.Name).Should().Equal(expectedNames, "D76 — hard delete, không còn dòng đã xóa trong DB");
        rows.Select(i => i.Order).Should().Equal(0, 1, 2);
    }

    [Fact(DisplayName = "FR-RCP-009/A2: Author khác thêm / sửa / xóa nguyên liệu → 403 RECIPE_FORBIDDEN, dữ liệu không đổi")]
    public async Task OtherAuthor_Returns403()
    {
        var owner = await _api.RegisterAuthorAsync();
        var other = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        var ingredientUrl = $"{IngredientsUrl(recipe.Id)}/{recipe.Ingredients[0].Id}";

        var responses = new[]
        {
            await _api.SendAsync(HttpMethod.Post, IngredientsUrl(recipe.Id), other.Token, IngredientBody()),
            await _api.SendAsync(HttpMethod.Put, ingredientUrl, other.Token, IngredientBody()),
            await _api.SendAsync(HttpMethod.Delete, ingredientUrl, other.Token),
        };

        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await response.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
                .Type.Should().Be(ErrorCodes.RecipeForbidden);
        }

        (await IngredientsInDbAsync(recipe.Id)).Select(i => i.Name).Should().Equal("Tôm", "Muối");
    }

    [Fact(DisplayName = "FR-RCP-009: Admin sửa nguyên liệu của recipe Author khác → 200")]
    public async Task Update_AsAdmin_Returns200()
    {
        var owner = await _api.RegisterAuthorAsync();
        var admin = await _api.RegisterAdminAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Put, $"{IngredientsUrl(recipe.Id)}/{recipe.Ingredients[1].Id}", admin.Token,
            IngredientBody("Muối hồng"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "FR-RCP-009/A1: recipe không tồn tại → 404 RECIPE_NOT_FOUND")]
    public async Task Add_UnknownRecipe_Returns404()
    {
        var owner = await _api.RegisterAuthorAsync();

        var response = await _api.SendAsync(HttpMethod.Post, IngredientsUrl(Guid.NewGuid()), owner.Token, IngredientBody());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
            .Type.Should().Be(ErrorCodes.RecipeNotFound);
    }

    [Fact(DisplayName = "FR-RCP-009/A1/D76: nguyên liệu của recipe KHÁC (hoặc không tồn tại) → 404 RECIPE_INGREDIENT_NOT_FOUND")]
    public async Task IngredientOfAnotherRecipe_Returns404()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipeA = await _api.CreateRecipeAsync(owner, category.Id);
        var recipeB = await _api.CreateRecipeAsync(owner, category.Id);

        var put = await _api.SendAsync(HttpMethod.Put, $"{IngredientsUrl(recipeA.Id)}/{recipeB.Ingredients[0].Id}", owner.Token, IngredientBody());
        var delete = await _api.SendAsync(HttpMethod.Delete, $"{IngredientsUrl(recipeA.Id)}/{Guid.NewGuid()}", owner.Token);

        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await put.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
            .Type.Should().Be(ErrorCodes.RecipeIngredientNotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await delete.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
            .Type.Should().Be(ErrorCodes.RecipeIngredientNotFound);
        (await IngredientsInDbAsync(recipeB.Id)).Should().HaveCount(2);
    }

    [Fact(DisplayName = "FR-RCP-009: chưa đăng nhập → 401")]
    public async Task Anonymous_Returns401()
    {
        var response = await _api.SendAsync(HttpMethod.Post, IngredientsUrl(Guid.NewGuid()), token: null, IngredientBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "FR-RCP-009/D3/D76: xóa nguyên liệu cuối cùng của recipe Published → 400 RECIPE_PUBLISH_INCOMPLETE")]
    public async Task Delete_LastIngredientOfPublished_Returns400()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id,
            ingredients: new[] { new { name = "Gạo", quantity = (decimal?)500m, unit = (string?)"g", notes = (string?)null } });
        await PublishInDbAsync(recipe.Id);

        var response = await _api.SendAsync(HttpMethod.Delete, $"{IngredientsUrl(recipe.Id)}/{recipe.Ingredients[0].Id}", owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
            .Type.Should().Be(ErrorCodes.RecipePublishIncomplete);
        (await IngredientsInDbAsync(recipe.Id)).Should().HaveCount(1);
    }

    [Fact(DisplayName = "FR-RCP-009: recipe Draft — xóa hết nguyên liệu được")]
    public async Task Delete_AllIngredientsOfDraft_Allowed()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        foreach (var ingredient in recipe.Ingredients)
        {
            (await _api.SendAsync(HttpMethod.Delete, $"{IngredientsUrl(recipe.Id)}/{ingredient.Id}", owner.Token))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await IngredientsInDbAsync(recipe.Id)).Should().BeEmpty();
    }

    [Fact(DisplayName = "FR-RCP-009/D8: thêm nguyên liệu xóa cache recipe:{slug} — GET chi tiết thấy ngay nguyên liệu mới")]
    public async Task Add_InvalidatesDetailCache()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        var detailUrl = $"/api/v1/recipes/{recipe.Slug}";
        (await _api.SendAsync(HttpMethod.Get, detailUrl, owner.Token)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await _api.SendAsync(HttpMethod.Post, IngredientsUrl(recipe.Id), owner.Token, IngredientBody()))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var detail = (await (await _api.SendAsync(HttpMethod.Get, detailUrl, owner.Token))
            .Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!;
        detail.Ingredients.Select(i => i.Name).Should().Equal("Tôm", "Muối", "Hành lá");
    }

    [Fact(DisplayName = "FR-RCP-009/D71: thêm/sửa/xóa nguyên liệu không đổi RowVersion recipe — PUT recipe bằng rowVersion cũ vẫn 200")]
    public async Task IngredientChanges_DoNotBumpRecipeRowVersion()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        (await _api.SendAsync(HttpMethod.Post, IngredientsUrl(recipe.Id), owner.Token, IngredientBody())).EnsureSuccessStatusCode();
        (await _api.SendAsync(HttpMethod.Put, $"{IngredientsUrl(recipe.Id)}/{recipe.Ingredients[1].Id}", owner.Token,
            IngredientBody("Muối biển", orderIndex: 0))).EnsureSuccessStatusCode();
        (await _api.SendAsync(HttpMethod.Delete, $"{IngredientsUrl(recipe.Id)}/{recipe.Ingredients[0].Id}", owner.Token)).EnsureSuccessStatusCode();

        var update = await _api.SendAsync(HttpMethod.Put, $"/api/v1/recipes/{recipe.Id}", owner.Token,
            new { title = "Gỏi cuốn sau khi sửa nguyên liệu", description = "Mô tả", categoryId = category.Id, prepTime = 20, cookTime = 10, servings = 4, difficulty = "Easy" },
            ifMatch: $"\"{recipe.RowVersion}\"");

        update.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "FR-RCP-009/D76: 5 request thêm nguyên liệu song song → đều 201, orderIndex 2..6 không trùng (khóa dòng recipe)")]
    public async Task ConcurrentAdds_AllSucceedWithUniqueOrder()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var responses = await Task.WhenAll(Enumerable.Range(1, 5).Select(i =>
            _api.SendAsync(HttpMethod.Post, IngredientsUrl(recipe.Id), owner.Token, IngredientBody($"Song song {i}"))));

        responses.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.Created);
        (await IngredientsInDbAsync(recipe.Id)).Select(i => i.Order).Should().Equal(0, 1, 2, 3, 4, 5, 6);
    }

    [Fact(DisplayName = "FR-RCP-009/D1: recipe đã xóa mềm → thao tác nguyên liệu trả 404")]
    public async Task SoftDeletedRecipe_Returns404()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
            (await db.Recipes.SingleAsync(r => r.Id == recipe.Id)).SoftDelete();
            await db.SaveChangesAsync();
        }

        var response = await _api.SendAsync(HttpMethod.Post, IngredientsUrl(recipe.Id), owner.Token, IngredientBody());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
