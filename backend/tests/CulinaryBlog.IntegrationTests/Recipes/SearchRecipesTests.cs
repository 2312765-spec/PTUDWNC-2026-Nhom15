using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Recipes;

/// <summary>
/// FR-SRCH-001 — GET /api/v1/recipes/search?q= (SRS mục 3.x + docs/decisions.md D4, D8).
/// Chạy trên Postgres thật vì truy vấn dùng to_tsvector/unaccent — cũng là test cho migration
/// AddUnaccentExtension (thiếu extension → 500).
/// Mỗi test dùng một token ngẫu nhiên trong Title để không đụng dữ liệu của test khác.
/// </summary>
public sealed class SearchRecipesTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "FR-SRCH-001: gõ không dấu \"pho bo\" tìm được \"Phở bò\" (unaccent)")]
    public async Task Search_Unaccented_MatchesAccentedTitle()
    {
        var token = NewToken();
        var recipeId = await SeedRecipeAsync($"Phở bò {token}", RecipeStatus.Published);

        var body = await SearchAsync($"pho bo {token}");

        body.Items.Should().ContainSingle(r => r.Id == recipeId);
        body.TotalCount.Should().Be(1);
    }

    [Fact(DisplayName = "FR-SRCH-001: trả URL ảnh đại diện, không phải tên kiểu RecipeImage")]
    public async Task Search_ReturnsPrimaryImageUrl()
    {
        var token = NewToken();
        const string imageUrl = "recipes/search-test/cover.webp";
        await SeedRecipeAsync($"Bún chả {token}", RecipeStatus.Published, imageUrl);

        var body = await SearchAsync(token);

        body.Items.Should().ContainSingle().Which.FeaturedImageUrl.Should().Be(imageUrl);
    }

    [Fact(DisplayName = "FR-SRCH-001: không trả công thức Draft")]
    public async Task Search_ExcludesDraft()
    {
        var token = NewToken();
        await SeedRecipeAsync($"Bánh xèo {token}", RecipeStatus.Draft);

        var body = await SearchAsync(token);

        body.Items.Should().BeEmpty();
    }

    [Theory(DisplayName = "FR-SRCH-001/D4: q rỗng hoặc < 2 ký tự → 400 VALIDATION_ERROR")]
    [InlineData("")]
    [InlineData("a")]
    public async Task Search_TooShort_Returns400(string q)
    {
        var response = await _client.GetAsync($"/api/v1/recipes/search?q={Uri.EscapeDataString(q)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem!.Type.Should().Be(ErrorCodes.ValidationError);
    }

    [Fact(DisplayName = "FR-SRCH-001/FR-SRCH-004: pageSize = 100 → clamp về 50, không trả lỗi")]
    public async Task Search_PageSizeOverMax_IsClamped()
    {
        var response = await _client.GetAsync("/api/v1/recipes/search?q=pho&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<RecipeSummaryDto>>(_jsonOptions);
        body!.PageSize.Should().Be(PagedResult<RecipeSummaryDto>.MaxPageSize);
    }

    [Theory(DisplayName = "FR-SRCH-001: ký tự toán tử tsquery trong q không gây 500")]
    [InlineData("pho <-> bo")]
    [InlineData("pho <2> bo")]
    [InlineData("'&|!():*\\")]
    public async Task Search_TsQueryOperators_DoesNotFail(string q)
    {
        var response = await _client.GetAsync($"/api/v1/recipes/search?q={Uri.EscapeDataString(q)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "FR-SRCH-001/D49: gõ \"dau phu\" tìm được \"Đậu phụ\" (unaccent bỏ cả chữ đ)")]
    public async Task Search_Unaccented_MatchesLetterD()
    {
        var token = NewToken();
        var recipeId = await SeedRecipeAsync($"Đậu phụ sốt cà {token}", RecipeStatus.Published);

        var body = await SearchAsync($"dau phu {token}");

        body.Items.Should().ContainSingle(r => r.Id == recipeId);
    }

    [Fact(DisplayName = "FR-SRCH-001/D49: xếp theo ts_rank — khớp ở Title (trọng số A) đứng trên khớp ở Description (B)")]
    public async Task Search_RanksTitleMatchAboveDescriptionMatch()
    {
        var token = NewToken();
        var descriptionMatch = await SeedRecipeAsync($"Canh chua {NewToken()}", RecipeStatus.Published, description: $"nấu với {token}");
        var titleMatch = await SeedRecipeAsync($"Gà nướng {token}", RecipeStatus.Published);

        var body = await SearchAsync(token);

        body.Items.Select(r => r.Id).Should().Equal(titleMatch, descriptionMatch);
        body.Items.Should().OnlyContain(r => r.RelevanceScore > 0);
        body.Items[0].RelevanceScore.Should().BeGreaterThan(body.Items[1].RelevanceScore!.Value);
    }

    [Fact(DisplayName = "FR-SRCH-001/D49: trigger cập nhật SearchVector khi Title đổi — từ mới tìm được, từ cũ hết")]
    public async Task Search_AfterTitleChange_TriggerUpdatesSearchVector()
    {
        var oldToken = NewToken();
        var newToken = NewToken();
        var recipeId = await SeedRecipeAsync($"Bánh cuốn {oldToken}", RecipeStatus.Published);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
            await db.Recipes.Where(r => r.Id == recipeId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Title, $"Bánh cuốn {newToken}"));
        }

        (await SearchAsync(newToken)).Items.Should().ContainSingle(r => r.Id == recipeId);
        (await SearchAsync(oldToken)).Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "FR-SRCH-001/D49: schema có GIN index trên SearchVector và trigger cập nhật")]
    public async Task Schema_HasGinIndexAndTrigger()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        var indexDef = await db.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'Recipes' AND indexname = 'IX_Recipes_SearchVector'")
            .SingleOrDefaultAsync();
        var triggerCount = await db.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM pg_trigger WHERE tgname = 'trg_recipes_search_vector' AND NOT tgisinternal")
            .SingleAsync();

        indexDef.Should().NotBeNull().And.Contain("USING gin").And.Contain("\"SearchVector\"");
        triggerCount.Should().Be(1);
    }

    private static string NewToken() => $"tk{Guid.NewGuid():N}"[..14];

    private async Task<PagedResult<RecipeSummaryDto>> SearchAsync(string q)
    {
        var response = await _client.GetAsync($"/api/v1/recipes/search?q={Uri.EscapeDataString(q)}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<RecipeSummaryDto>>(_jsonOptions);
        body.Should().NotBeNull();
        return body!;
    }

    private async Task<string> RegisterAuthorAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = $"search-{Guid.NewGuid():N}@example.com", password = "Str0ng!Pass1", displayName = "Tác giả test" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions);
        return body!.User.Id;
    }

    private async Task<Guid> SeedRecipeAsync(string title, RecipeStatus status, string? imageUrl = null, string description = "mô tả")
    {
        var authorId = await RegisterAuthorAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        var unique = Guid.NewGuid().ToString("N")[..12];
        var category = Category.Create($"Danh mục search {unique}", $"danh-muc-search-{unique}", "desc", null, 0);
        db.Categories.Add(category);

        var recipe = Recipe.Create(
            title, $"recipe-{Guid.NewGuid():N}", description, 10, 30, 2,
            RecipeDifficulty.Easy, category.Id, authorId, status: status);
        if (imageUrl is not null)
            recipe.AttachImage(imageUrl, isPrimary: true);
        db.Recipes.Add(recipe);

        await db.SaveChangesAsync();
        return recipe.Id;
    }
}
