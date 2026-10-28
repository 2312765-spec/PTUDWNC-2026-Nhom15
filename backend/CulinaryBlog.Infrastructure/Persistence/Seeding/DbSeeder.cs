using Bogus;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed dữ liệu mẫu Development — SRS dòng 295 (Bogus, 50 recipe / 5 tác giả), roadmap S4 (có cả
/// Draft và Archived), roadmap mục Auth (1 tài khoản Admin). Plan: docs/plans/DEV-SEED-du-lieu-mau-that.md.
///
/// Nội dung lấy từ <see cref="SeedRecipeCatalog"/>; Bogus (seed cố định) chỉ chọn tác giả cho từng món.
/// Chạy lại an toàn trên DB đã có dữ liệu thật: user nhận diện theo email seed, category dùng lại
/// theo slug, recipe chỉ tạo khi tác giả seed chưa có recipe nào.
/// </summary>
public static class DbSeeder
{
    private const int _randomSeed = 20260920;

    public static async Task SeedAsync(
        CulinaryBlogDbContext db,
        UserManager<ApplicationUser> userManager,
        ISlugHelper slugHelper,
        CancellationToken ct = default)
    {
        var authors = new List<ApplicationUser>();
        foreach (var seed in SeedRecipeCatalog.Authors)
        {
            authors.Add(await EnsureUserAsync(userManager, seed.Email, seed.DisplayName, seed.Bio, Roles.Author));
        }

        await EnsureUserAsync(userManager, SeedRecipeCatalog.AdminEmail, SeedRecipeCatalog.AdminDisplayName, bio: null, Roles.Admin);

        var authorIds = authors.Select(a => a.Id).ToList();
        if (await db.Recipes.IgnoreQueryFilters().AnyAsync(r => authorIds.Contains(r.AuthorId), ct))
        {
            return; // đã seed recipe trước đó.
        }

        var categoryIds = await EnsureCategoriesAsync(db, slugHelper, ct);

        var usedSlugs = (await db.Recipes.IgnoreQueryFilters().Select(r => r.Slug).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var faker = new Faker("vi") { Random = new Randomizer(_randomSeed) };

        var recipes = new List<Recipe>();
        for (var i = 0; i < SeedRecipeCatalog.Recipes.Count; i++)
        {
            recipes.Add(BuildRecipe(
                SeedRecipeCatalog.Recipes[i],
                status: (RecipeStatus)(i % 3), // rải đều Draft/Published/Archived (roadmap S4).
                slug: UniqueSlug(slugHelper.Generate(SeedRecipeCatalog.Recipes[i].Title), usedSlugs),
                categoryId: categoryIds[SeedRecipeCatalog.Recipes[i].CategoryName],
                authorId: faker.PickRandom(authors).Id));
        }

        db.Recipes.AddRange(recipes);
        await db.SaveChangesAsync(ct);
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager, string email, string displayName, string? bio, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = displayName,
                Bio = bio,
                EmailConfirmed = true,
            };
            ThrowIfFailed(await userManager.CreateAsync(user, SeedRecipeCatalog.DevPassword), email);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            ThrowIfFailed(await userManager.AddToRoleAsync(user, role), email);
        }

        return user;
    }

    /// <summary>Trả về map tên category → Id; dùng lại category có sẵn theo slug, chỉ tạo cái còn thiếu.</summary>
    private static async Task<Dictionary<string, Guid>> EnsureCategoriesAsync(
        CulinaryBlogDbContext db, ISlugHelper slugHelper, CancellationToken ct)
    {
        var existing = await db.Categories.IgnoreQueryFilters().ToDictionaryAsync(c => c.Slug, ct);
        var ids = new Dictionary<string, Guid>();

        foreach (var seed in SeedRecipeCatalog.Categories)
        {
            var slug = slugHelper.Generate(seed.Name);
            if (!existing.TryGetValue(slug, out var category))
            {
                category = Category.Create(seed.Name, slug, seed.Description);
                db.Categories.Add(category);
            }

            ids[seed.Name] = category.Id;
        }

        await db.SaveChangesAsync(ct);
        return ids;
    }

    private static Recipe BuildRecipe(SeedRecipe seed, RecipeStatus status, string slug, Guid categoryId, string authorId)
    {
        var recipe = Recipe.Create(
            title: seed.Title,
            slug: slug,
            description: seed.Description,
            prepTime: seed.PrepTime,
            cookTime: seed.CookTime,
            servings: seed.Servings,
            difficulty: seed.Difficulty,
            categoryId: categoryId,
            authorId: authorId);

        for (var s = 0; s < seed.Steps.Count; s++)
        {
            var step = seed.Steps[s];
            recipe.AddStep(s + 1, step.Title, step.Description, step.TimerMinutes);
        }

        foreach (var ingredient in seed.Ingredients)
        {
            recipe.AddIngredient(ingredient.Name, ingredient.Quantity, ingredient.Unit, ingredient.Notes);
        }

        recipe.AttachImage($"https://placehold.co/800x600/png?text={slug}", altText: seed.Title);

        // Qua domain method để D3 (≥ 1 step và ≥ 1 ingredient) được kiểm tra thật.
        if (status != RecipeStatus.Draft)
        {
            recipe.Publish();
        }

        if (status == RecipeStatus.Archived)
        {
            recipe.Archive();
        }

        return recipe;
    }

    /// <summary>D10: trùng slug thì thêm hậu tố -2, -3…</summary>
    private static string UniqueSlug(string baseSlug, HashSet<string> usedSlugs)
    {
        var slug = baseSlug;
        for (var n = 2; !usedSlugs.Add(slug); n++)
        {
            slug = $"{baseSlug}-{n}";
        }

        return slug;
    }

    private static void ThrowIfFailed(IdentityResult result, string email)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Seed user {email} thất bại: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }
}
