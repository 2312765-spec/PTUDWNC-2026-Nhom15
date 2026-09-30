using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Common;

/// <summary>
/// CONS-006 — model EF (entity + Configurations) phải khớp migration. Trước đây code bị sửa lệch
/// (Nutrition bị Ignore, RecipeIngredient đổi Amount/Preparation, mất MediumUrl/ThumbnailUrl…)
/// trong khi PendingModelChangesWarning bị tắt, nên "dotnet ef migrations add" kế tiếp sẽ DROP
/// 11 cột. Test này bắt lỗi đó ngay trên CI.
/// </summary>
public sealed class SchemaDriftTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact(DisplayName = "CONS-006: model EF khớp migration — không có thay đổi chưa tạo migration")]
    public void Model_HasNoPendingChanges()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        db.Database.HasPendingModelChanges().Should().BeFalse(
            "entity/config đã đổi mà chưa có migration — chạy 'dotnet ef migrations add' và ĐỌC KỸ nó trước khi commit");
    }

    [Fact(DisplayName = "SRS 7.2.1/7.4: lưu và đọc lại Nutrition + Ingredient (Quantity/Notes/OrderIndex) + Step trên Postgres thật")]
    public async Task Recipe_WithNutritionIngredientsSteps_RoundTrips()
    {
        var recipeId = await SeedAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var recipe = await db.Recipes
            .AsNoTracking()
            .Include(r => r.Ingredients)
            .Include(r => r.Steps)
            .SingleAsync(r => r.Id == recipeId);

        recipe.Nutrition.Calories.Should().Be(450.5m);
        recipe.Nutrition.Carbohydrates.Should().Be(60m);
        recipe.Nutrition.Sodium.Should().Be(1200m);
        recipe.Ingredients.Should().HaveCount(2);
        recipe.Ingredients.OrderBy(i => i.OrderIndex).Select(i => i.Name).Should().Equal("Thịt bò", "Hành lá");
        recipe.Ingredients.Single(i => i.Name == "Thịt bò").Quantity.Should().Be(500.125m);
        recipe.Ingredients.Single(i => i.Name == "Hành lá").Notes.Should().Be("thái nhỏ");
        recipe.Steps.Should().ContainSingle().Which.Title.Should().Be("Sơ chế");
    }

    [Fact(DisplayName = "SRS 7.2.1: recipe không khai báo dinh dưỡng → lưu được, đọc lại các giá trị đều null")]
    public async Task Recipe_WithoutNutrition_RoundTripsAsNulls()
    {
        var recipeId = await SeedAsync(withNutrition: false);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var recipe = await db.Recipes.AsNoTracking().SingleAsync(r => r.Id == recipeId);

        recipe.Nutrition.Should().NotBeNull();
        recipe.Nutrition.Calories.Should().BeNull();
    }

    private async Task<Guid> SeedAsync(bool withNutrition = true)
    {
        var client = factory.CreateClient();
        var email = $"drift-{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsync(
            "/api/v1/auth/register",
            System.Net.Http.Json.JsonContent.Create(new { email, password = "Str0ng!Pass1", displayName = "Tác giả" }));
        register.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var authorId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();

        var unique = Guid.NewGuid().ToString("N")[..12];
        var category = Category.Create($"Danh mục {unique}", $"danh-muc-{unique}");
        db.Categories.Add(category);

        var recipe = Recipe.Create(
            "Bò lúc lắc", $"bo-luc-lac-{unique}", "mô tả", 10, 20, 2,
            RecipeDifficulty.Easy, category.Id, authorId);
        recipe.AddIngredient("Thịt bò", 500.125m, "gram");
        recipe.AddIngredient("Hành lá", null, null, "thái nhỏ");
        recipe.AddStep(1, "Sơ chế", "Rửa sạch thịt bò.");
        if (withNutrition)
        {
            recipe.SetNutrition(450.5m, 30m, 60m, 12m, sodium: 1200m);
        }

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        return recipe.Id;
    }
}
