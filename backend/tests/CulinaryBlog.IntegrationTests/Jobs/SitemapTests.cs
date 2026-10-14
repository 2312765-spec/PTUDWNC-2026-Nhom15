using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Seo;
using CulinaryBlog.Application.Seo.Commands.GenerateSitemap;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Jobs;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.IntegrationTests.Recipes.Support;
using FluentAssertions;
using FluentValidation;
using Hangfire;
using Hangfire.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Jobs;

/// <summary>
/// FR-JOB-003, NFR-SEO-003 — docs/decisions.md D51–D53. Postgres thật (Testcontainers) +
/// FakeFileStorageService. Testing không chạy Hangfire server (D45) → test resolve job từ DI rồi tự gọi.
/// Các test trong class dùng chung một database, nên kiểm tra theo slug của chính test thay vì đếm tổng.
/// </summary>
public sealed class SitemapTests(RecipeImagesApiFactory factory) : IClassFixture<RecipeImagesApiFactory>
{
    private static readonly XNamespace Ns = SitemapXmlBuilder.Namespace;
    private const string BaseUrl = RecipeImagesApiFactory.SiteBaseUrl;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string? _authorId;

    [Fact(DisplayName = "FR-JOB-003/D1: chỉ recipe Published vào sitemap — Draft, Archived, đã soft delete bị loại")]
    public async Task Sitemap_ContainsOnlyPublishedRecipes()
    {
        var category = await SeedCategoryAsync();
        var published = await SeedRecipeAsync(category, RecipeStatus.Published);
        var draft = await SeedRecipeAsync(category, RecipeStatus.Draft);
        var archived = await SeedRecipeAsync(category, RecipeStatus.Archived);
        var deleted = await SeedRecipeAsync(category, RecipeStatus.Published, softDelete: true);

        var locs = Locs(await RunJobAsync());

        locs.Should().Contain($"{BaseUrl}/recipes/{published}");
        locs.Should().NotContain($"{BaseUrl}/recipes/{draft}");
        locs.Should().NotContain($"{BaseUrl}/recipes/{archived}");
        locs.Should().NotContain($"{BaseUrl}/recipes/{deleted}");
    }

    [Fact(DisplayName = "FR-JOB-003: số URL recipe trong sitemap = số recipe Published chưa xóa trong DB")]
    public async Task Sitemap_RecipeCount_EqualsPublishedCount()
    {
        var category = await SeedCategoryAsync();
        await SeedRecipeAsync(category, RecipeStatus.Published);
        await SeedRecipeAsync(category, RecipeStatus.Draft);

        var sitemap = await RunJobAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var publishedCount = await db.Recipes.CountAsync(r => r.Status == RecipeStatus.Published);

        Locs(sitemap).Count(l => l.StartsWith($"{BaseUrl}/recipes/", StringComparison.Ordinal))
            .Should().Be(publishedCount);
    }

    [Fact(DisplayName = "FR-JOB-003/D2: category chưa xóa vào sitemap, category đã soft delete bị loại")]
    public async Task Sitemap_ExcludesSoftDeletedCategories()
    {
        var active = await SeedCategoryAsync();
        var deleted = await SeedCategoryAsync(softDelete: true);

        var locs = Locs(await RunJobAsync());

        locs.Should().Contain($"{BaseUrl}/categories/{active.Slug}");
        locs.Should().NotContain($"{BaseUrl}/categories/{deleted.Slug}");
    }

    [Fact(DisplayName = "FR-JOB-003/D52: có 3 trang tĩnh /, /recipes, /categories — không có /search, /auth, /dashboard")]
    public async Task Sitemap_ContainsStaticPages()
    {
        var locs = Locs(await RunJobAsync());

        locs.Should().Contain([$"{BaseUrl}/", $"{BaseUrl}/recipes", $"{BaseUrl}/categories"]);
        locs.Should().NotContain(l => l.Contains("/search") || l.Contains("/auth") || l.Contains("/dashboard"));
    }

    [Fact(DisplayName = "NFR-SEO-003: mỗi <url> có đủ loc (tuyệt đối), lastmod, changefreq, priority đúng chuẩn sitemaps.org")]
    public async Task Sitemap_EveryUrl_HasRequiredElements()
    {
        var category = await SeedCategoryAsync();
        await SeedRecipeAsync(category, RecipeStatus.Published);

        var sitemap = await RunJobAsync();

        sitemap.Root!.Name.Should().Be(Ns + "urlset");
        var urls = sitemap.Root.Elements(Ns + "url").ToList();
        urls.Should().NotBeEmpty();
        foreach (var url in urls)
        {
            Uri.TryCreate(url.Element(Ns + "loc")?.Value, UriKind.Absolute, out var loc).Should().BeTrue();
            loc!.GetLeftPart(UriPartial.Authority).Should().Be(BaseUrl);

            DateTime.TryParseExact(url.Element(Ns + "lastmod")?.Value, "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out _).Should().BeTrue();

            url.Element(Ns + "changefreq")?.Value.Should().BeOneOf("daily", "weekly");

            decimal.TryParse(url.Element(Ns + "priority")?.Value, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var priority).Should().BeTrue();
            priority.Should().BeInRange(0m, 1m);
        }
    }

    [Fact(DisplayName = "FR-JOB-003: lastmod của recipe = UpdatedAt (sau khi sửa), priority 0.8, changefreq weekly")]
    public async Task Sitemap_RecipeEntry_UsesUpdatedAt()
    {
        var category = await SeedCategoryAsync();
        var slug = await SeedRecipeAsync(category, RecipeStatus.Published);
        var updatedAt = new DateTime(2026, 9, 1, 8, 30, 0, DateTimeKind.Utc);
        await SetRecipeUpdatedAtAsync(slug, updatedAt);

        var url = (await RunJobAsync()).Root!.Elements(Ns + "url")
            .Single(u => u.Element(Ns + "loc")!.Value == $"{BaseUrl}/recipes/{slug}");

        url.Element(Ns + "lastmod")!.Value.Should().Be("2026-09-01T08:30:00Z");
        url.Element(Ns + "changefreq")!.Value.Should().Be("weekly");
        url.Element(Ns + "priority")!.Value.Should().Be("0.8");
    }

    [Fact(DisplayName = "FR-JOB-003/D52: ghi seo/sitemap.xml, application/xml; chạy 2 lần → ghi đè cùng key, không sinh object mới")]
    public async Task Job_WritesFixedKey_AndOverwrites()
    {
        var savesBefore = factory.FileStorage.Saves.Count;

        await RunJobAsync();
        await RunJobAsync();

        var saves = factory.FileStorage.Saves.Skip(savesBefore).ToList();
        saves.Should().HaveCount(2).And.AllBeEquivalentTo((GenerateSitemapCommandHandler.ObjectKey, "application/xml"));
        factory.FileStorage.Objects.Keys.Count(k => k.EndsWith("/sitemap.xml", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact(DisplayName = "FR-JOB-003/D52: thiếu Seo:SiteBaseUrl → job lỗi (ValidationException), không ghi sitemap")]
    public async Task Job_MissingSiteBaseUrl_ThrowsAndWritesNothing()
    {
        var savesBefore = factory.FileStorage.Saves.Count;
        using var scope = factory.Services.CreateScope();
        var job = new SitemapGenerationJob(
            scope.ServiceProvider.GetRequiredService<ISender>(),
            new ConfigurationBuilder().Build());

        var act = () => job.ExecuteAsync(CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        factory.FileStorage.Saves.Count.Should().Be(savesBefore);
    }

    [Theory(DisplayName = "FR-JOB-003/D52: SiteBaseUrl không phải URL tuyệt đối http(s) → ValidationException")]
    [InlineData("culinary.test")]
    [InlineData("ftp://culinary.test")]
    [InlineData("/relative")]
    public async Task Command_InvalidSiteBaseUrl_ThrowsValidation(string siteBaseUrl)
    {
        using var scope = factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var act = () => sender.Send(new GenerateSitemapCommand(siteBaseUrl));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact(DisplayName = "FR-JOB-003/D53: job retry 2 lần (SRS) và chống chạy trùng bằng DisableConcurrentExecution")]
    public void Job_HasRetryAndConcurrencyAttributes()
    {
        var method = typeof(SitemapGenerationJob).GetMethod(nameof(SitemapGenerationJob.ExecuteAsync))!;
        var attributes = typeof(SitemapGenerationJob).GetCustomAttributes(inherit: true)
            .Concat(method.GetCustomAttributes(inherit: true)).ToList();

        attributes.OfType<AutomaticRetryAttribute>().Single().Attempts.Should().Be(2);
        attributes.OfType<DisableConcurrentExecutionAttribute>().Should().ContainSingle();
    }

    [Fact(DisplayName = "FR-JOB-003: recurring job đăng ký id sitemap-generation, cron 0 2 * * *, múi giờ UTC")]
    public void RegisterRecurringJobs_RegistersSitemapAt0200Utc()
    {
        var manager = new RecordingRecurringJobManager();

        manager.RegisterRecurringJobs();

        var (id, job, cron, options) = manager.Registered.Should().ContainSingle().Subject;
        id.Should().Be("sitemap-generation");
        cron.Should().Be("0 2 * * *");
        options.TimeZone.Should().Be(TimeZoneInfo.Utc);
        job.Type.Should().Be<SitemapGenerationJob>();
        job.Method.Name.Should().Be(nameof(SitemapGenerationJob.ExecuteAsync));
    }

    // ---------------- Helpers ----------------

    /// <summary>Chạy đúng đường Hangfire sẽ chạy: resolve job từ DI → ExecuteAsync → MediatR command.</summary>
    private async Task<XDocument> RunJobAsync()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<SitemapGenerationJob>();
            await job.ExecuteAsync(CancellationToken.None);
        }

        var bytes = factory.FileStorage.Objects[FakeFileStorageService.UrlFor(GenerateSitemapCommandHandler.ObjectKey)];
        return XDocument.Load(new MemoryStream(bytes));
    }

    private static List<string> Locs(XDocument sitemap) =>
        sitemap.Root!.Elements(Ns + "url").Select(u => u.Element(Ns + "loc")!.Value).ToList();

    private async Task<Category> SeedCategoryAsync(bool softDelete = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        var suffix = Guid.NewGuid().ToString("N");
        var category = Category.Create($"Danh mục {suffix}", $"danh-muc-{suffix}");
        if (softDelete)
        {
            category.Delete();
        }

        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    /// <summary>Seed trực tiếp qua DbContext, trả về slug. AuthorId có FK tới user → cần user thật.</summary>
    private async Task<string> SeedRecipeAsync(Category category, RecipeStatus status, bool softDelete = false)
    {
        var authorId = await GetAuthorIdAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        var recipe = Recipe.Create(
            title: "Công thức sitemap",
            slug: $"cong-thuc-sitemap-{Guid.NewGuid():N}",
            categoryId: category.Id,
            authorId: authorId,
            status: status);
        if (softDelete)
        {
            recipe.SoftDelete();
        }

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        return recipe.Slug;
    }

    private async Task<string> GetAuthorIdAsync()
    {
        if (_authorId is not null)
        {
            return _authorId;
        }

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = $"sitemap-{Guid.NewGuid():N}@example.com", password = "Str0ng!Pass1", displayName = "Tác giả sitemap" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        return _authorId = body!.User.Id;
    }

    private async Task SetRecipeUpdatedAtAsync(string slug, DateTime updatedAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        await db.Recipes.Where(r => r.Slug == slug)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.UpdatedAt, updatedAt));
    }

    /// <summary>Thay IRecurringJobManager của Hangfire — ghi lại lời gọi AddOrUpdate, không cần JobStorage.</summary>
    private sealed class RecordingRecurringJobManager : IRecurringJobManager
    {
        public List<(string Id, Job Job, string Cron, RecurringJobOptions Options)> Registered { get; } = [];

        public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options) =>
            Registered.Add((recurringJobId, job, cronExpression, options));

        public void Trigger(string recurringJobId) => throw new NotSupportedException();

        public void RemoveIfExists(string recurringJobId) => throw new NotSupportedException();
    }
}
