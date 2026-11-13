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
///
/// Ảnh món ăn (<see cref="SeedImageAssets"/>, plan docs/plans/DEV-SEED-anh-mon-an.md) gắn ở bước riêng chạy MỌI lần,
/// kể cả DB đã seed trước khi có ảnh thật: thay ảnh placehold.co cũ và ảnh seed bản cũ (D98), giữ nguyên ảnh
/// người dùng tự upload.
/// </summary>
public static class DbSeeder
{
    private const int RandomSeed = 20260920;

    public static async Task SeedAsync(
        CulinaryBlogDbContext db,
        UserManager<ApplicationUser> userManager,
        ISlugHelper slugHelper,
        IFileStorageService fileStorage,
        IBackgroundJobService backgroundJobs,
        CancellationToken ct = default)
    {
        await SeedUsersAndRecipesAsync(db, userManager, slugHelper, ct);
        await EnsureSeedImagesAsync(db, slugHelper, fileStorage, backgroundJobs, ct);
    }

    private static async Task SeedUsersAndRecipesAsync(
        CulinaryBlogDbContext db,
        UserManager<ApplicationUser> userManager,
        ISlugHelper slugHelper,
        CancellationToken ct)
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
        var faker = new Faker("vi") { Random = new Randomizer(RandomSeed) };

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

    /// <summary>
    /// Gắn ảnh thật cho recipe của tác giả seed. Chỉ đụng recipe chưa có ảnh hoặc chỉ có ảnh placehold.co (dữ liệu
    /// seed cũ); recipe đã có ảnh khác (người dùng tự upload) giữ nguyên. Món chưa có ảnh trong
    /// <see cref="SeedImageAssets"/> thì chỉ bỏ placeholder — không gắn ảnh sai món.
    /// Upload đi đúng đường của ảnh người dùng: <see cref="IFileStorageService.UploadAsync"/> (tên GUID, kiểm tra magic
    /// bytes — CONS-007) vào <c>recipes/{id}</c>, sau đó enqueue tạo medium/thumbnail (FR-JOB-002).
    /// </summary>
    private static async Task EnsureSeedImagesAsync(
        CulinaryBlogDbContext db,
        ISlugHelper slugHelper,
        IFileStorageService fileStorage,
        IBackgroundJobService backgroundJobs,
        CancellationToken ct)
    {
        var seedEmails = SeedRecipeCatalog.Authors.Select(a => a.Email).ToList();
        var authorIds = await db.Users.Where(u => seedEmails.Contains(u.Email!)).Select(u => u.Id).ToListAsync(ct);
        var catalogTitles = SeedRecipeCatalog.Recipes.Select(r => r.Title).ToHashSet(StringComparer.Ordinal);

        var recipes = await db.Recipes
            .Include(r => r.Images)
            .Where(r => authorIds.Contains(r.AuthorId))
            .ToListAsync(ct);

        var attached = new List<(Guid RecipeId, Guid ImageId)>();
        var retiredFiles = new List<string>();
        foreach (var recipe in recipes.Where(r => catalogTitles.Contains(r.Title)))
        {
            if (recipe.Images.Any(i => !IsPlaceholder(i.OriginalUrl)))
            {
                var retired = await FindRetiredSeedImageAsync(recipe, fileStorage, ct);
                if (retired is null)
                {
                    continue; // ảnh seed hiện hành hoặc ảnh người dùng upload — giữ nguyên.
                }

                // D98: ảnh seed bản cũ → gỡ khỏi aggregate, xóa file gốc + biến thể (D43) rồi gắn ảnh mới bên dưới.
                db.RecipeImages.Remove(recipe.RemoveImage(retired.Id));
                retiredFiles.AddRange(new[] { retired.OriginalUrl, retired.MediumUrl, retired.ThumbnailUrl, retired.CardUrl }
                    .OfType<string>().Where(url => url.Length > 0));
            }

            foreach (var placeholder in recipe.Images.ToList())
            {
                db.RecipeImages.Remove(recipe.RemoveImage(placeholder.Id));
            }

            var slug = slugHelper.Generate(recipe.Title);
            await using var content = SeedImageAssets.Open(slug);
            if (content is null)
            {
                continue;
            }

            var url = await fileStorage.UploadAsync(
                content, SeedImageAssets.FileName(slug), SeedImageAssets.ContentType, $"recipes/{recipe.Id}", ct);
            var image = recipe.AttachImage(url, altText: recipe.Title);
            db.RecipeImages.Add(image); // D31 — Add tường minh: con mới của aggregate đang được theo dõi.
            attached.Add((recipe.Id, image.Id));
        }

        await db.SaveChangesAsync(ct);

        // D88: ảnh đã chạy FR-JOB-002 trước khi có biến thể card (có thumbnail, chưa có card) → chạy lại job để bổ
        // sung card. Ảnh vừa gắn ở trên chưa có thumbnail nên không bị enqueue hai lần.
        var missingCard = recipes
            .SelectMany(r => r.Images.Where(i => i.ThumbnailUrl is not null && i.CardUrl is null).Select(i => (r.Id, i.Id)));

        foreach (var (recipeId, imageId) in attached.Concat(missingCard))
        {
            backgroundJobs.EnqueueGenerateImageVariants(recipeId, imageId);
        }

        foreach (var url in retiredFiles)
        {
            backgroundJobs.EnqueueDeleteImageFile(url);
        }
    }

    /// <summary>
    /// D98 — ảnh duy nhất của recipe, alt = tên món (dấu hiệu ảnh seed) và nội dung trùng SHA-256 một ảnh seed bản cũ
    /// (<see cref="SeedImageAssets.RetiredSha256"/>). Nhận diện theo nội dung nên ảnh người dùng upload không bao giờ khớp.
    /// Object không đọc được (storage dev đã bị xóa…) thì coi như không phải ảnh cũ — để nguyên.
    /// </summary>
    private static async Task<RecipeImage?> FindRetiredSeedImageAsync(
        Recipe recipe, IFileStorageService fileStorage, CancellationToken ct)
    {
        if (recipe.Images.Count != 1 || recipe.Images.Single() is not { } image || image.AltText != recipe.Title)
        {
            return null;
        }

        try
        {
            await using var content = await fileStorage.DownloadAsync(image.OriginalUrl, ct);
            return SeedImageAssets.IsRetired(content) ? image : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static bool IsPlaceholder(string url) => url.Contains("placehold.co", StringComparison.OrdinalIgnoreCase);

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
