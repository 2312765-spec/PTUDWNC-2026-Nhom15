using System.Net.Http.Json;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Common;

/// <summary>
/// D34 + D4 — optimistic concurrency qua RowVersion trên PostgreSQL thật. PostgreSQL không tự sinh
/// RowVersion như SQL Server, nên nếu code không tự đổi thì "WHERE RowVersion = ''" luôn đúng và
/// người lưu sau âm thầm ghi đè người lưu trước (lost update).
/// </summary>
public sealed class ConcurrencyTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact(DisplayName = "D34/D4: hai người cùng sửa một recipe → người lưu sau nhận DbUpdateConcurrencyException (→ 409)")]
    public async Task TwoConcurrentEdits_SecondSave_Throws()
    {
        var recipeId = await SeedRecipeAsync();

        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        // Cả hai cùng đọc bản gốc.
        var recipeA = await dbA.Recipes.SingleAsync(r => r.Id == recipeId);
        var recipeB = await dbB.Recipes.SingleAsync(r => r.Id == recipeId);

        recipeA.UpdateDetails("Bản của A", "mô tả A", 10, 20, 2, RecipeDifficulty.Easy, recipeA.CategoryId);
        await dbA.SaveChangesAsync();

        recipeB.UpdateDetails("Bản của B", "mô tả B", 10, 20, 2, RecipeDifficulty.Easy, recipeB.CategoryId);
        var saveB = () => dbB.SaveChangesAsync();

        await saveB.Should().ThrowAsync<DbUpdateConcurrencyException>();

        using var verify = factory.Services.CreateScope();
        var title = await verify.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>()
            .Recipes.Where(r => r.Id == recipeId).Select(r => r.Title).SingleAsync();
        title.Should().Be("Bản của A", "bản của A không được bị B ghi đè");
    }

    [Fact(DisplayName = "D34: RowVersion khác rỗng khi tạo và đổi sau mỗi lần lưu")]
    public async Task RowVersion_IsSetOnCreate_AndChangesOnEverySave()
    {
        var recipeId = await SeedRecipeAsync();

        var afterCreate = await ReadRowVersionAsync(recipeId);
        afterCreate.Should().NotBeEmpty();

        await EditAsync(recipeId, r => r.Archive());
        var afterFirstEdit = await ReadRowVersionAsync(recipeId);

        await EditAsync(recipeId, r => r.MoveToDraft());
        var afterSecondEdit = await ReadRowVersionAsync(recipeId);

        afterFirstEdit.Should().NotEqual(afterCreate);
        afterSecondEdit.Should().NotEqual(afterFirstEdit);
    }

    [Fact(DisplayName = "D34: chỉ sửa Nutrition (owned) cũng đổi RowVersion của Recipe")]
    public async Task EditingOwnedNutritionOnly_ChangesRecipeRowVersion()
    {
        var recipeId = await SeedRecipeAsync();
        var before = await ReadRowVersionAsync(recipeId);

        await EditAsync(recipeId, r => r.SetNutrition(300m, 20m, 30m, 10m));

        (await ReadRowVersionAsync(recipeId)).Should().NotEqual(before);
    }

    [Fact(DisplayName = "SRS 7.1: UpdatedAt do AuditInterceptor tự điền, kể cả khi domain method không set")]
    public async Task AuditInterceptor_SetsUpdatedAt_OnModify()
    {
        var recipeId = await SeedRecipeAsync(withImage: true);

        // Recipe.UpdateImage (alt text) KHÔNG tự set RecipeImage.UpdatedAt — chỉ interceptor mới điền.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
            var recipe = await db.Recipes.Include(r => r.Images).SingleAsync(r => r.Id == recipeId);
            recipe.UpdateImage(recipe.Images.Single().Id, altText: "ảnh mới");
            await db.SaveChangesAsync();
        }

        using var verify = factory.Services.CreateScope();
        var updatedAt = await verify.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>()
            .RecipeImages.Where(i => i.RecipeId == recipeId).Select(i => i.UpdatedAt).SingleAsync();
        updatedAt.Should().NotBeNull();
    }

    private async Task EditAsync(Guid recipeId, Action<Recipe> edit)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var recipe = await db.Recipes.SingleAsync(r => r.Id == recipeId);
        edit(recipe);
        await db.SaveChangesAsync();
    }

    private async Task<byte[]> ReadRowVersionAsync(Guid recipeId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>()
            .Recipes.Where(r => r.Id == recipeId).Select(r => r.RowVersion).SingleAsync();
    }

    private async Task<Guid> SeedRecipeAsync(bool withImage = false)
    {
        var email = $"concurrency-{Guid.NewGuid():N}@example.com";
        var register = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, password = "Str0ng!Pass1", displayName = "Tác giả" });
        register.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var authorId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();

        var unique = Guid.NewGuid().ToString("N")[..12];
        var category = Category.Create(
            name: $"Danh mục {unique}",
            slug: $"danh-muc-{unique}",
            description: "mô tả",
            imageUrl: null,
            orderIndex: 0
        );
        db.Categories.Add(category);

        var recipe = Recipe.Create(
            "Cơm tấm", $"com-tam-{unique}", "mô tả", 10, 20, 2,
            RecipeDifficulty.Easy, category.Id, authorId);
        if (withImage)
        {
            recipe.AttachImage($"recipes/{unique}/cover.webp", "ảnh cũ");
        }

        db.Recipes.Add(recipe);

        await db.SaveChangesAsync();
        return recipe.Id;
    }
}
