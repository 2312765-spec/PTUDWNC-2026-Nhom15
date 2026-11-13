using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Persistence.Seeding;
using CulinaryBlog.IntegrationTests.Common;
using CulinaryBlog.IntegrationTests.Recipes.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Seeding;

/// <summary>
/// DEV-SEED ảnh món ăn — plan docs/plans/DEV-SEED-anh-mon-an.md. Ảnh Wikimedia Commons đã resize, nhúng vào
/// Infrastructure (<see cref="SeedImageAssets"/>); seeder upload qua <see cref="IFileStorageService"/> rồi enqueue
/// tạo thumbnail (FR-JOB-002) như ảnh người dùng upload. DB mới: seed 2 lần, kiểm tra không upload trùng.
/// </summary>
public sealed class DbSeederImagesTests(DbSeederImagesFixture fixture) : IClassFixture<DbSeederImagesFixture>
{
    [Fact(DisplayName = "DEV-SEED ảnh: mỗi asset ứng với đúng một món trong catalog")]
    public void Assets_MatchCatalogTitles()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var slugHelper = scope.ServiceProvider.GetRequiredService<ISlugHelper>();
        var catalogSlugs = SeedRecipeCatalog.Recipes.Select(r => slugHelper.Generate(r.Title)).ToHashSet();

        SeedImageAssets.Slugs.Should().NotBeEmpty();
        SeedImageAssets.Slugs.Should().OnlyContain(slug => catalogSlugs.Contains(slug));
        foreach (var slug in SeedImageAssets.Slugs)
        {
            using var stream = SeedImageAssets.Open(slug);
            stream.Should().NotBeNull();
            stream!.Length.Should().BeInRange(1, 5 * 1024 * 1024, "CONS-007: ảnh ≤ 5 MB");
        }
    }

    [Fact(DisplayName = "DEV-SEED ảnh: không còn URL placehold.co ở bất kỳ recipe seed nào")]
    public async Task Seed_HasNoPlaceholderImages()
    {
        var recipes = await SeededRecipesAsync();

        recipes.SelectMany(r => r.Images).Should().NotContain(i => i.OriginalUrl.Contains("placehold.co"));
    }

    [Fact(DisplayName = "DEV-SEED ảnh: món có asset → đúng 1 ảnh Primary trên storage, alt = tên món; món chưa có asset → không ảnh")]
    public async Task Seed_RecipesWithAsset_HaveOnePrimaryStoredImage()
    {
        var recipes = await SeededRecipesAsync();
        using var scope = fixture.Factory.Services.CreateScope();
        var slugHelper = scope.ServiceProvider.GetRequiredService<ISlugHelper>();

        foreach (var recipe in recipes)
        {
            if (SeedImageAssets.Slugs.Contains(slugHelper.Generate(recipe.Title)))
            {
                recipe.Images.Should().ContainSingle(recipe.Title);
                var image = recipe.Images.Single();
                image.IsPrimary.Should().BeTrue();
                image.AltText.Should().Be(recipe.Title);
                fixture.Storage.Objects.Should().ContainKey(image.OriginalUrl);
            }
            else
            {
                recipe.Images.Should().BeEmpty(recipe.Title);
            }
        }
    }

    [Fact(DisplayName = "DEV-SEED ảnh: upload WebP vào thư mục recipes/{id} như upload thường; seed 2 lần không upload trùng")]
    public void Seed_UploadsEachAssetOnce()
    {
        fixture.Storage.Uploads.Should().HaveCount(SeedImageAssets.Slugs.Count);
        fixture.Storage.Uploads.Should().OnlyContain(u => u.ContentType == "image/webp" && u.Folder.StartsWith("recipes/"));
    }

    [Fact(DisplayName = "DEV-SEED ảnh / FR-JOB-002: enqueue tạo medium + thumbnail cho từng ảnh seed")]
    public async Task Seed_EnqueuesVariantGeneration()
    {
        var images = (await SeededRecipesAsync()).SelectMany(r => r.Images.Select(i => (r.Id, i.Id))).ToList();

        fixture.Jobs.GenerateImageVariants.Should().BeEquivalentTo(images);
    }

    private async Task<List<Recipe>> SeededRecipesAsync()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var seedEmails = SeedRecipeCatalog.Authors.Select(a => a.Email).ToList();
        var seedAuthorIds = await db.Users.Where(u => seedEmails.Contains(u.Email!)).Select(u => u.Id).ToListAsync();

        return await db.Recipes.AsNoTracking().IgnoreQueryFilters()
            .Include(r => r.Images)
            .Where(r => seedAuthorIds.Contains(r.AuthorId))
            .ToListAsync();
    }
}

public sealed class DbSeederImagesFixture : IAsyncLifetime
{
    public PostgresApiFactory Factory { get; } = new();

    public FakeFileStorageService Storage { get; } = new();

    public RecordingBackgroundJobService Jobs { get; } = new();

    public async Task InitializeAsync()
    {
        await Factory.InitializeAsync();
        for (var run = 0; run < 2; run++)
        {
            await DbSeederRunner.RunAsync(Factory, Storage, Jobs);
        }
    }

    public async Task DisposeAsync() => await ((IAsyncLifetime)Factory).DisposeAsync();
}

/// <summary>
/// DB dev đã seed TRƯỚC khi có ảnh thật (ảnh placehold.co) — tình trạng máy của cả nhóm. Seeder vẫn dừng sớm ở
/// phần tạo recipe, nhưng bước ảnh phải thay placeholder; ảnh người dùng tự upload thì giữ nguyên.
/// </summary>
public sealed class DbSeederLegacyImagesTests(DbSeederLegacyImagesFixture fixture) : IClassFixture<DbSeederLegacyImagesFixture>
{
    [Fact(DisplayName = "DEV-SEED ảnh: DB seed cũ — ảnh placehold.co được thay bằng ảnh thật trên storage")]
    public async Task LegacyPlaceholder_IsReplaced()
    {
        var recipe = await LoadAsync(fixture.PlaceholderRecipeId);

        recipe.Images.Should().ContainSingle();
        recipe.Images.Single().OriginalUrl.Should().StartWith("https://fake-minio.local/");
        recipe.Images.Single().IsPrimary.Should().BeTrue();
    }

    [Fact(DisplayName = "DEV-SEED ảnh: DB seed cũ — món chưa có asset thì bỏ placeholder, không gắn ảnh sai")]
    public async Task LegacyPlaceholder_WithoutAsset_IsRemoved()
    {
        var recipe = await LoadAsync(fixture.NoAssetRecipeId);

        recipe.Images.Should().BeEmpty();
    }

    [Fact(DisplayName = "DEV-SEED ảnh: ảnh người dùng tự upload cho recipe seed được giữ nguyên")]
    public async Task UserUploadedImage_IsUntouched()
    {
        var recipe = await LoadAsync(fixture.UserImageRecipeId);

        recipe.Images.Should().ContainSingle().Which.OriginalUrl.Should().Be(DbSeederLegacyImagesFixture.UserImageUrl);
    }

    [Fact(DisplayName = "DEV-SEED ảnh / D88: ảnh đã có medium + thumbnail nhưng chưa có card → enqueue job để bổ sung card")]
    public void ImageWithoutCard_IsEnqueuedForCardVariant()
    {
        fixture.Jobs.GenerateImageVariants.Should().Contain((fixture.UserImageRecipeId, fixture.UserImageId));
    }

    [Fact(DisplayName = "DEV-SEED ảnh / D98: ảnh seed bản cũ (đã thay) được thay bằng ảnh mới, file cũ + biến thể bị xóa")]
    public async Task RetiredSeedImage_IsReplaced()
    {
        var recipe = await LoadAsync(fixture.RetiredRecipeId);

        var image = recipe.Images.Should().ContainSingle().Subject;
        image.Id.Should().NotBe(fixture.RetiredImageId);
        image.IsPrimary.Should().BeTrue();
        image.AltText.Should().Be(recipe.Title);
        fixture.Storage.Objects.Should().ContainKey(image.OriginalUrl);
        fixture.Jobs.DeleteImageFileUrls.Should().BeEquivalentTo(fixture.RetiredImageUrls);
        fixture.Jobs.GenerateImageVariants.Should().Contain((fixture.RetiredRecipeId, image.Id));
    }

    [Fact(DisplayName = "DEV-SEED ảnh / D98: ảnh hiện hành không bị thay lại ở lần seed sau")]
    public void CurrentSeedImage_IsNotReplacedAgain()
    {
        // Seed chạy 2 lần: lần 2 thấy ảnh mới (không thuộc danh sách đã thay) → không upload thêm.
        fixture.Storage.Uploads.Should().HaveCount(3, "ảnh cũ do fixture tạo + 1 ảnh thay placeholder + 1 ảnh thay ảnh seed cũ");
    }

    [Fact(DisplayName = "DEV-SEED ảnh: DB seed cũ — không tạo thêm recipe seed")]
    public async Task Legacy_DoesNotCreateMoreRecipes()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        (await db.Recipes.IgnoreQueryFilters().CountAsync()).Should().Be(4);
    }

    private async Task<Recipe> LoadAsync(Guid id)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        return await db.Recipes.AsNoTracking().IgnoreQueryFilters().Include(r => r.Images).SingleAsync(r => r.Id == id);
    }
}

public sealed class DbSeederLegacyImagesFixture : IAsyncLifetime
{
    public const string UserImageUrl = "https://fake-minio.local/culinary-blog/recipes/user-upload.jpg";

    public PostgresApiFactory Factory { get; } = new();

    public Guid PlaceholderRecipeId { get; private set; }

    public Guid NoAssetRecipeId { get; private set; }

    public Guid UserImageRecipeId { get; private set; }

    /// <summary>Ảnh đã chạy FR-JOB-002 trước D88 (có medium + thumbnail, chưa có card).</summary>
    public Guid UserImageId { get; private set; }

    public RecordingBackgroundJobService Jobs { get; } = new();

    public FakeFileStorageService Storage { get; } = new();

    /// <summary>D98 — recipe đang mang ảnh seed bản cũ (file <c>Seeding/Fixtures/retired-seed-image.webp</c>).</summary>
    public Guid RetiredRecipeId { get; private set; }

    public Guid RetiredImageId { get; private set; }

    public List<string> RetiredImageUrls { get; } = [];

    public async Task InitializeAsync()
    {
        await Factory.InitializeAsync();

        var retiredBytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Seeding", "Fixtures", "retired-seed-image.webp"));
        using (var upload = new MemoryStream(retiredBytes))
        {
            RetiredImageUrls.Add(await Storage.UploadAsync(upload, "old.webp", "image/webp", "recipes/old"));
        }

        RetiredImageUrls.Add("https://fake-minio.local/culinary-blog/recipes/old/medium.webp");
        RetiredImageUrls.Add("https://fake-minio.local/culinary-blog/recipes/old/thumb.webp");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var slugHelper = scope.ServiceProvider.GetRequiredService<ISlugHelper>();

            var seedAuthor = SeedRecipeCatalog.Authors[0];
            var author = new ApplicationUser { UserName = seedAuthor.Email, Email = seedAuthor.Email, DisplayName = seedAuthor.DisplayName, EmailConfirmed = true };
            (await userManager.CreateAsync(author, SeedRecipeCatalog.DevPassword)).Succeeded.Should().BeTrue();

            var category = Category.Create("Món nước", "mon-nuoc");
            db.Categories.Add(category);

            var withAsset = SeedRecipeCatalog.Recipes.First(r => SeedImageAssets.Slugs.Contains(slugHelper.Generate(r.Title)));
            var withoutAsset = SeedRecipeCatalog.Recipes.First(r => !SeedImageAssets.Slugs.Contains(slugHelper.Generate(r.Title)));
            var userImage = SeedRecipeCatalog.Recipes.Last(r => SeedImageAssets.Slugs.Contains(slugHelper.Generate(r.Title)));

            Recipe Legacy(string title, string imageUrl)
            {
                var recipe = Recipe.Create(title: title, slug: slugHelper.Generate(title), description: "Seed cũ",
                    categoryId: category.Id, authorId: author.Id);
                recipe.AttachImage(imageUrl, altText: title);
                db.Recipes.Add(recipe);
                return recipe;
            }

            PlaceholderRecipeId = Legacy(withAsset.Title, "https://placehold.co/800x600/png?text=old").Id;
            NoAssetRecipeId = Legacy(withoutAsset.Title, "https://placehold.co/800x600/png?text=old-2").Id;
            var withUserImage = Legacy(userImage.Title, UserImageUrl);
            UserImageRecipeId = withUserImage.Id;
            UserImageId = withUserImage.Images.Single().Id;
            withUserImage.SetImageVariants(UserImageId, "https://fake-minio.local/m.webp", "https://fake-minio.local/t.webp");

            // Món có asset hiện hành, khác 3 món trên, đang mang ảnh seed bản cũ đã có medium + thumbnail.
            var retired = SeedRecipeCatalog.Recipes
                .Where(r => SeedImageAssets.Slugs.Contains(slugHelper.Generate(r.Title)))
                .First(r => r.Title != withAsset.Title && r.Title != userImage.Title);
            var withRetired = Legacy(retired.Title, RetiredImageUrls[0]);
            RetiredRecipeId = withRetired.Id;
            RetiredImageId = withRetired.Images.Single().Id;
            withRetired.SetImageVariants(RetiredImageId, RetiredImageUrls[1], RetiredImageUrls[2]);
            await db.SaveChangesAsync();
        }

        for (var run = 0; run < 2; run++)
        {
            await DbSeederRunner.RunAsync(Factory, Storage, Jobs);
        }
    }

    public async Task DisposeAsync() => await ((IAsyncLifetime)Factory).DisposeAsync();
}

internal static class DbSeederRunner
{
    public static async Task RunAsync(PostgresApiFactory factory, IFileStorageService storage, IBackgroundJobService jobs)
    {
        using var scope = factory.Services.CreateScope();
        await DbSeeder.SeedAsync(
            scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>(),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<ISlugHelper>(),
            storage,
            jobs);
    }
}
