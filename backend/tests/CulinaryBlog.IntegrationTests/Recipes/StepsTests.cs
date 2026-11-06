using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Recipes.DTOs;
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
/// FR-RCP-010 — POST/PUT/DELETE /api/v1/recipes/{id}/steps/{stepId?}. D6 (server đánh số + renumber),
/// D31 (thêm con vào aggregate đã track), D71 (không đổi RowVersion recipe), D73 (hard delete, 2 pha, khóa).
/// Chạy trên PostgreSQL thật vì unique index (RecipeId, StepNumber) chỉ lộ lỗi ở DB thật.
/// </summary>
public sealed class StepsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private readonly RecipeCommandClient _api = new(factory);

    private static object FourSteps => Enumerable.Range(1, 4)
        .Select(i => new { title = $"Bước {i}", description = $"Mô tả {i}", timerMinutes = (int?)null })
        .ToArray();

    private static string StepsUrl(Guid recipeId) => $"/api/v1/recipes/{recipeId}/steps";

    private static object StepBody(string title = "Lật bánh", int? stepNumber = null) =>
        new { title, description = "Lật khi mặt dưới vàng giòn", timerMinutes = 3, stepNumber };

    private async Task<(int Number, string Title)[]> StepsInDbAsync(Guid recipeId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var rows = await db.RecipeSteps.IgnoreQueryFilters()
            .Where(s => s.RecipeId == recipeId)
            .OrderBy(s => s.StepNumber)
            .Select(s => new { s.StepNumber, s.Title })
            .ToListAsync();
        return [.. rows.Select(r => (r.StepNumber, r.Title))];
    }

    [Fact(DisplayName = "FR-RCP-010/D6/D31: thêm bước → 201, StepNumber = Max + 1, bỏ qua stepNumber client gửi")]
    public async Task Add_Returns201WithNextNumber()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Post, StepsUrl(recipe.Id), owner.Token, StepBody(stepNumber: 1));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var step = (await response.Content.ReadFromJsonAsync<RecipeStepDto>(RecipeCommandClient.JsonOptions))!;
        step.StepNumber.Should().Be(3);
        step.Title.Should().Be("Lật bánh");
        step.TimerMinutes.Should().Be(3);
        (await StepsInDbAsync(recipe.Id)).Select(s => s.Number).Should().Equal(1, 2, 3);
    }

    [Theory(DisplayName = "FR-RCP-010/D6: xóa bước đầu / giữa / cuối → 204, các bước còn lại đánh số liên tục")]
    [InlineData(0, new[] { "Bước 2", "Bước 3", "Bước 4" })]
    [InlineData(1, new[] { "Bước 1", "Bước 3", "Bước 4" })]
    [InlineData(3, new[] { "Bước 1", "Bước 2", "Bước 3" })]
    public async Task Delete_RenumbersRemaining(int index, string[] expectedTitles)
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id, FourSteps);

        var response = await _api.SendAsync(HttpMethod.Delete, $"{StepsUrl(recipe.Id)}/{recipe.Steps[index].Id}", owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var rows = await StepsInDbAsync(recipe.Id);
        rows.Select(s => s.Title).Should().Equal(expectedTitles, "D73 — hard delete, không còn dòng đã xóa trong DB");
        rows.Select(s => s.Number).Should().Equal(1, 2, 3);
    }

    [Theory(DisplayName = "FR-RCP-010/D6: đổi vị trí (lên đầu, xuống cuối, đổi chỗ 2 bước kề nhau) → 200, toàn bộ bước theo thứ tự mới")]
    [InlineData(3, 1, new[] { "Bước 4 sửa", "Bước 1", "Bước 2", "Bước 3" })]
    [InlineData(0, 4, new[] { "Bước 2", "Bước 3", "Bước 4", "Bước 1 sửa" })]
    [InlineData(0, 2, new[] { "Bước 2", "Bước 1 sửa", "Bước 3", "Bước 4" })]
    public async Task Update_MovesStep(int index, int newPosition, string[] expectedTitles)
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id, FourSteps);
        var target = recipe.Steps[index];

        var response = await _api.SendAsync(HttpMethod.Put, $"{StepsUrl(recipe.Id)}/{target.Id}", owner.Token,
            new { title = target.Title + " sửa", description = "Mô tả đã sửa", timerMinutes = (int?)null, stepNumber = newPosition });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var steps = (await response.Content.ReadFromJsonAsync<List<RecipeStepDto>>(RecipeCommandClient.JsonOptions))!;
        steps.Select(s => s.Title).Should().Equal(expectedTitles);
        steps.Select(s => s.StepNumber).Should().Equal(1, 2, 3, 4);
        (await StepsInDbAsync(recipe.Id)).Select(s => s.Title).Should().Equal(expectedTitles);
    }

    [Fact(DisplayName = "FR-RCP-010: sửa bước không gửi stepNumber → giữ vị trí, đổi nội dung")]
    public async Task Update_ContentOnly_KeepsPosition()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Put, $"{StepsUrl(recipe.Id)}/{recipe.Steps[0].Id}", owner.Token,
            new { title = "Sơ chế kỹ", description = "Rửa tôm bằng nước muối", timerMinutes = 15, imageUrl = "https://cdn.example.com/so-che.jpg" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = (await response.Content.ReadFromJsonAsync<List<RecipeStepDto>>(RecipeCommandClient.JsonOptions))![0];
        first.Title.Should().Be("Sơ chế kỹ");
        first.StepNumber.Should().Be(1);
        first.TimerMinutes.Should().Be(15);
        first.ImageUrl.Should().Be("https://cdn.example.com/so-che.jpg");
    }

    [Fact(DisplayName = "FR-RCP-010/D73: stepNumber vượt số bước → 400 VALIDATION_ERROR")]
    public async Task Update_PositionOutOfRange_Returns400()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Put, $"{StepsUrl(recipe.Id)}/{recipe.Steps[0].Id}", owner.Token,
            StepBody(stepNumber: 5));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
            .Type.Should().Be(ErrorCodes.ValidationError);
    }

    [Fact(DisplayName = "FR-RCP-010/D4: body sai (description rỗng) → 400 VALIDATION_ERROR")]
    public async Task Add_InvalidBody_Returns400()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Post, StepsUrl(recipe.Id), owner.Token,
            new { title = "Bước", description = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(RecipeCommandClient.JsonOptions))!;
        problem.Type.Should().Be(ErrorCodes.ValidationError);
        problem.Errors.Keys.Should().Contain("Description");
    }

    [Fact(DisplayName = "FR-RCP-010/A2: Author khác thêm / sửa / xóa bước → 403 RECIPE_FORBIDDEN, dữ liệu không đổi")]
    public async Task OtherAuthor_Returns403()
    {
        var owner = await _api.RegisterAuthorAsync();
        var other = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        var stepUrl = $"{StepsUrl(recipe.Id)}/{recipe.Steps[0].Id}";

        var responses = new[]
        {
            await _api.SendAsync(HttpMethod.Post, StepsUrl(recipe.Id), other.Token, StepBody()),
            await _api.SendAsync(HttpMethod.Put, stepUrl, other.Token, StepBody()),
            await _api.SendAsync(HttpMethod.Delete, stepUrl, other.Token),
        };

        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await response.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
                .Type.Should().Be(ErrorCodes.RecipeForbidden);
        }

        (await StepsInDbAsync(recipe.Id)).Select(s => s.Title).Should().Equal("Sơ chế", "Cuốn");
    }

    [Fact(DisplayName = "FR-RCP-010: Admin thêm bước vào recipe của Author → 201")]
    public async Task Add_AsAdmin_Returns201()
    {
        var owner = await _api.RegisterAuthorAsync();
        var admin = await _api.RegisterAdminAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var response = await _api.SendAsync(HttpMethod.Post, StepsUrl(recipe.Id), admin.Token, StepBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "FR-RCP-010/A1: recipe không tồn tại → 404 RECIPE_NOT_FOUND")]
    public async Task Add_UnknownRecipe_Returns404()
    {
        var owner = await _api.RegisterAuthorAsync();

        var response = await _api.SendAsync(HttpMethod.Post, StepsUrl(Guid.NewGuid()), owner.Token, StepBody());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
            .Type.Should().Be(ErrorCodes.RecipeNotFound);
    }

    [Fact(DisplayName = "FR-RCP-010/D73: bước của recipe KHÁC (hoặc không tồn tại) → 404 RECIPE_STEP_NOT_FOUND")]
    public async Task StepOfAnotherRecipe_Returns404()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipeA = await _api.CreateRecipeAsync(owner, category.Id);
        var recipeB = await _api.CreateRecipeAsync(owner, category.Id);

        var put = await _api.SendAsync(HttpMethod.Put, $"{StepsUrl(recipeA.Id)}/{recipeB.Steps[0].Id}", owner.Token, StepBody());
        var delete = await _api.SendAsync(HttpMethod.Delete, $"{StepsUrl(recipeA.Id)}/{Guid.NewGuid()}", owner.Token);

        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await put.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
            .Type.Should().Be(ErrorCodes.RecipeStepNotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await StepsInDbAsync(recipeB.Id)).Should().HaveCount(2);
    }

    [Fact(DisplayName = "FR-RCP-010: chưa đăng nhập → 401")]
    public async Task Anonymous_Returns401()
    {
        var response = await _api.SendAsync(HttpMethod.Post, StepsUrl(Guid.NewGuid()), token: null, StepBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "FR-RCP-010/D3/D73: xóa bước cuối cùng của recipe Published → 400 RECIPE_PUBLISH_INCOMPLETE")]
    public async Task Delete_LastStepOfPublished_Returns400()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id,
            new[] { new { title = "Bước duy nhất", description = "Trộn đều", timerMinutes = (int?)null } });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
            var entity = await db.Recipes.Include(r => r.Steps).Include(r => r.Ingredients).SingleAsync(r => r.Id == recipe.Id);
            entity.Publish();
            await db.SaveChangesAsync();
        }

        var response = await _api.SendAsync(HttpMethod.Delete, $"{StepsUrl(recipe.Id)}/{recipe.Steps[0].Id}", owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(RecipeCommandClient.JsonOptions))!
            .Type.Should().Be(ErrorCodes.RecipePublishIncomplete);
        (await StepsInDbAsync(recipe.Id)).Should().HaveCount(1);
    }

    [Fact(DisplayName = "FR-RCP-010/D8: thêm bước xóa cache recipe:{slug} — GET chi tiết thấy ngay bước mới")]
    public async Task Add_InvalidatesDetailCache()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        var detailUrl = $"/api/v1/recipes/{recipe.Slug}";
        (await _api.SendAsync(HttpMethod.Get, detailUrl, owner.Token)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await _api.SendAsync(HttpMethod.Post, StepsUrl(recipe.Id), owner.Token, StepBody())).StatusCode.Should().Be(HttpStatusCode.Created);

        var detail = (await (await _api.SendAsync(HttpMethod.Get, detailUrl, owner.Token))
            .Content.ReadFromJsonAsync<RecipeDetailDto>(RecipeCommandClient.JsonOptions))!;
        detail.Steps.Select(s => s.Title).Should().Equal("Sơ chế", "Cuốn", "Lật bánh");
    }

    [Fact(DisplayName = "FR-RCP-010/D71: sửa bước không đổi RowVersion của recipe — PUT recipe bằng rowVersion cũ vẫn 200")]
    public async Task StepChanges_DoNotBumpRecipeRowVersion()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);
        (await _api.SendAsync(HttpMethod.Post, StepsUrl(recipe.Id), owner.Token, StepBody())).EnsureSuccessStatusCode();
        (await _api.SendAsync(HttpMethod.Delete, $"{StepsUrl(recipe.Id)}/{recipe.Steps[0].Id}", owner.Token)).EnsureSuccessStatusCode();

        var update = await _api.SendAsync(HttpMethod.Put, $"/api/v1/recipes/{recipe.Id}", owner.Token,
            new { title = "Gỏi cuốn sau khi sửa bước", description = "Mô tả", categoryId = category.Id, prepTime = 20, cookTime = 10, servings = 4, difficulty = "Easy" },
            ifMatch: $"\"{recipe.RowVersion}\"");

        update.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "FR-RCP-010/D73: 5 request thêm bước song song → đều 201, số thứ tự 3..7 không trùng (khóa dòng recipe)")]
    public async Task ConcurrentAdds_AllSucceedWithUniqueNumbers()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        var responses = await Task.WhenAll(Enumerable.Range(1, 5).Select(i =>
            _api.SendAsync(HttpMethod.Post, StepsUrl(recipe.Id), owner.Token, StepBody($"Song song {i}"))));

        responses.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.Created);
        (await StepsInDbAsync(recipe.Id)).Select(s => s.Number).Should().Equal(1, 2, 3, 4, 5, 6, 7);
    }

    [Fact(DisplayName = "FR-RCP-010: recipe đã xóa mềm (D1) → thao tác bước trả 404")]
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

        var response = await _api.SendAsync(HttpMethod.Post, StepsUrl(recipe.Id), owner.Token, StepBody());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "FR-RCP-010: recipe Draft — xóa hết bước được (Draft chưa cần đủ điều kiện publish)")]
    public async Task Delete_AllStepsOfDraft_Allowed()
    {
        var owner = await _api.RegisterAuthorAsync();
        var category = await _api.CreateCategoryAsync();
        var recipe = await _api.CreateRecipeAsync(owner, category.Id);

        foreach (var step in recipe.Steps)
        {
            (await _api.SendAsync(HttpMethod.Delete, $"{StepsUrl(recipe.Id)}/{step.Id}", owner.Token))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await StepsInDbAsync(recipe.Id)).Should().BeEmpty();
        recipe.Status.Should().Be((short)RecipeStatus.Draft);
    }
}
