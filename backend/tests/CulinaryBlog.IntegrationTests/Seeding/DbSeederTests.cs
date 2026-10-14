using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Seeding;
using CulinaryBlog.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Seeding;

/// <summary>
/// Seed dữ liệu mẫu Development — SRS dòng 295 (Bogus, 50 recipe / 5 tác giả), roadmap S4 (có cả
/// Draft và Archived), roadmap mục Auth (1 tài khoản Admin). Plan: docs/plans/DEV-SEED-du-lieu-mau-that.md.
///
/// Fixture dựng sẵn dữ liệu "thật" (1 category, 1 user, 1 recipe) rồi gọi SeedAsync HAI lần —
/// mọi test chỉ đọc kết quả, nên thứ tự chạy test không ảnh hưởng.
/// </summary>
public sealed class DbSeederTests(DbSeederFixture fixture) : IClassFixture<DbSeederFixture>
{
    [Fact(DisplayName = "DEV-SEED: tạo đúng 5 tác giả seed, tất cả có role Author")]
    public async Task Seed_CreatesFiveAuthors_WithAuthorRole()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var authors = await userManager.GetUsersInRoleAsync(Roles.Author);
        var seededAuthors = authors.Where(u => SeedRecipeCatalog.Authors.Any(a => a.Email == u.Email)).ToList();

        seededAuthors.Should().HaveCount(5);
        seededAuthors.Should().OnlyContain(u => u.IsActive && u.EmailConfirmed && !string.IsNullOrWhiteSpace(u.Bio));
    }

    [Fact(DisplayName = "DEV-SEED: tạo 1 tài khoản Admin seed có role Admin")]
    public async Task Seed_CreatesAdmin_WithAdminRole()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);

        admins.Should().ContainSingle(u => u.Email == SeedRecipeCatalog.AdminEmail);
    }

    [Theory(DisplayName = "DEV-SEED: tài khoản seed đăng nhập được bằng mật khẩu dev")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Seed_Accounts_CanLogin(int authorIndex)
    {
        var email = authorIndex < 0 ? SeedRecipeCatalog.AdminEmail : SeedRecipeCatalog.Authors[authorIndex].Email;
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = SeedRecipeCatalog.DevPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "DEV-SEED: đủ 50 recipe seed, rải cả Draft / Published / Archived")]
    public async Task Seed_CreatesFiftyRecipes_AcrossAllStatuses()
    {
        var recipes = await SeededRecipesAsync();

        recipes.Should().HaveCount(50);
        recipes.Select(r => r.Status).Distinct().Should()
            .BeEquivalentTo([RecipeStatus.Draft, RecipeStatus.Published, RecipeStatus.Archived]);
    }

    [Fact(DisplayName = "DEV-SEED/D3: mọi recipe seed có ≥ 1 step và ≥ 1 ingredient; Published/Archived có PublishedAt")]
    public async Task Seed_Recipes_SatisfyPublishRule()
    {
        var recipes = await SeededRecipesAsync();

        recipes.Should().OnlyContain(r => r.Steps.Count >= 1 && r.Ingredients.Count >= 1);
        recipes.Where(r => r.Status != RecipeStatus.Draft).Should().OnlyContain(r => r.PublishedAt != null);
    }

    [Fact(DisplayName = "DEV-SEED: nội dung lấy từ SeedRecipeCatalog — không còn chữ sinh ngẫu nhiên")]
    public async Task Seed_Content_ComesFromCatalog()
    {
        var recipes = await SeededRecipesAsync();
        var catalogTitles = SeedRecipeCatalog.Recipes.Select(r => r.Title).ToHashSet();
        var catalogIngredients = SeedRecipeCatalog.Recipes
            .SelectMany(r => r.Ingredients).Select(i => i.Name).ToHashSet();

        SeedRecipeCatalog.Recipes.Should().HaveCount(50);
        catalogTitles.Should().HaveCount(50, "mỗi món trong catalog phải khác tên");
        recipes.Should().OnlyContain(r => catalogTitles.Contains(r.Title));
        recipes.SelectMany(r => r.Ingredients).Should().OnlyContain(i => catalogIngredients.Contains(i.Name));
    }

    [Fact(DisplayName = "DEV-SEED/D10: slug recipe seed duy nhất")]
    public async Task Seed_RecipeSlugs_AreUnique()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        var slugs = await db.Recipes.IgnoreQueryFilters().Select(r => r.Slug).ToListAsync();

        slugs.Should().OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "DEV-SEED: gọi SeedAsync 2 lần không sinh trùng dữ liệu")]
    public async Task Seed_IsIdempotent()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        (await db.Users.CountAsync()).Should().Be(1 + 5 + 1); // user có sẵn + 5 tác giả + 1 admin
        (await db.Recipes.IgnoreQueryFilters().CountAsync()).Should().Be(1 + 50);
    }

    [Fact(DisplayName = "DEV-SEED: dùng lại category có sẵn, tổng cộng 8 category, không trùng slug")]
    public async Task Seed_ReusesExistingCategories()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        var categories = await db.Categories.IgnoreQueryFilters().ToListAsync();

        categories.Should().HaveCount(8);
        categories.Should().ContainSingle(c => c.Id == fixture.ExistingCategoryId);
        categories.Select(c => c.Slug).Should().OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "DEV-SEED: không đụng tới user và recipe có sẵn")]
    public async Task Seed_LeavesExistingDataUntouched()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        var recipe = await db.Recipes.AsNoTracking()
            .Include(r => r.Steps).Include(r => r.Ingredients)
            .SingleAsync(r => r.Id == fixture.ExistingRecipeId);

        recipe.Title.Should().Be(DbSeederFixture.ExistingRecipeTitle);
        recipe.Status.Should().Be(RecipeStatus.Draft);
        recipe.Steps.Should().BeEmpty();
        recipe.Ingredients.Should().BeEmpty();
        (await db.Recipes.CountAsync(r => r.AuthorId == fixture.ExistingUserId)).Should().Be(1);
    }

    private async Task<List<Recipe>> SeededRecipesAsync()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        return await db.Recipes.AsNoTracking().IgnoreQueryFilters()
            .Include(r => r.Steps).Include(r => r.Ingredients)
            .Where(r => r.AuthorId != fixture.ExistingUserId)
            .ToListAsync();
    }
}

/// <summary>
/// Dựng dữ liệu có sẵn (mô phỏng DB dev đã có category "Món chính", 1 user thật và recipe test
/// của họ — đúng tình trạng DB dev 2026-10-14) rồi chạy DbSeeder hai lần.
/// </summary>
public sealed class DbSeederFixture : IAsyncLifetime
{
    public const string ExistingRecipeTitle = "Recipe test của user thật";

    public PostgresApiFactory Factory { get; } = new();

    public Guid ExistingCategoryId { get; private set; }

    public string ExistingUserId { get; private set; } = string.Empty;

    public Guid ExistingRecipeId { get; private set; }

    public async Task InitializeAsync()
    {
        await Factory.InitializeAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var category = Category.Create("Món chính", "mon-chinh");
            db.Categories.Add(category);

            var user = new ApplicationUser { UserName = "real.user@example.com", Email = "real.user@example.com", DisplayName = "User thật" };
            (await userManager.CreateAsync(user, "RealUser123!")).Succeeded.Should().BeTrue();
            (await userManager.AddToRoleAsync(user, Roles.Author)).Succeeded.Should().BeTrue();

            var recipe = Recipe.Create(
                title: ExistingRecipeTitle, slug: "recipe-test-cua-user-that", description: "Recipe có sẵn trước khi seed",
                categoryId: category.Id, authorId: user.Id);
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();

            ExistingCategoryId = category.Id;
            ExistingUserId = user.Id;
            ExistingRecipeId = recipe.Id;
        }

        for (var run = 0; run < 2; run++)
        {
            using var scope = Factory.Services.CreateScope();
            await DbSeeder.SeedAsync(
                scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>(),
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                scope.ServiceProvider.GetRequiredService<ISlugHelper>());
        }
    }

    public async Task DisposeAsync() => await ((IAsyncLifetime)Factory).DisposeAsync();
}
