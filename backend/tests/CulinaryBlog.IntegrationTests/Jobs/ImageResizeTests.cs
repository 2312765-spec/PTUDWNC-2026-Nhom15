using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Common.Files;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Jobs;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.IntegrationTests.Files;
using CulinaryBlog.IntegrationTests.Recipes.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Jobs;

/// <summary>
/// FR-JOB-002 — docs/decisions.md D40–D44. Postgres thật (Testcontainers) + FakeFileStorageService
/// giữ nội dung object. Môi trường Testing không chạy Hangfire server (D45), nên test kiểm tra
/// việc enqueue qua <see cref="RecordingBackgroundJobService"/> rồi tự chạy job/command.
/// </summary>
public sealed class ImageResizeTests(RecipeImagesApiFactory factory) : IClassFixture<RecipeImagesApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    private sealed record UploadImageResponse(Guid ImageId, string OriginalUrl, string? AltText, bool IsPrimary);

    [Fact(DisplayName = "FR-JOB-002/D44: upload ảnh thành công → enqueue job resize với đúng recipeId + imageId")]
    public async Task Upload_EnqueuesGenerateImageVariants()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(userId);

        var uploaded = await UploadAsync(recipeId, TestImages.Png(1200, 900), "image/png", token);

        factory.BackgroundJobs.GenerateImageVariants.Should().Contain((recipeId, uploaded.ImageId));
    }

    [Fact(DisplayName = "FR-JOB-002/D40/D41: job chạy → MediumUrl 800×600 + ThumbnailUrl 300×300, cả hai WebP, ghi vào DB")]
    public async Task Job_GeneratesMediumAndThumbnail()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(userId);
        var uploaded = await UploadAsync(recipeId, TestImages.Png(1200, 900), "image/png", token);

        await RunJobAsync(recipeId, uploaded.ImageId);

        var image = await GetImageAsync(uploaded.ImageId);
        image.MediumUrl.Should().NotBeNullOrWhiteSpace().And.Contain($"/recipes/{recipeId}/").And.EndWith(".webp");
        image.ThumbnailUrl.Should().NotBeNullOrWhiteSpace().And.Contain($"/recipes/{recipeId}/").And.EndWith(".webp");
        image.OriginalUrl.Should().Be(uploaded.OriginalUrl);

        var medium = factory.FileStorage.Objects[image.MediumUrl!];
        var thumbnail = factory.FileStorage.Objects[image.ThumbnailUrl!];
        ImageSignature.Detect(medium)!.ContentType.Should().Be("image/webp");
        ImageSignature.Detect(thumbnail)!.ContentType.Should().Be("image/webp");

        TestImages.Size(medium).Should().Be((800, 600));
        TestImages.Size(thumbnail).Should().Be((300, 300));
    }

    [Fact(DisplayName = "FR-JOB-002/D44: chạy job lần 2 → no-op (idempotent), không upload thêm file, URL giữ nguyên")]
    public async Task Job_RunTwice_SecondRunIsNoOp()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(userId);
        var uploaded = await UploadAsync(recipeId, TestImages.Png(640, 480), "image/png", token);

        await RunJobAsync(recipeId, uploaded.ImageId);
        var afterFirst = await GetImageAsync(uploaded.ImageId);
        var uploadsAfterFirst = UploadsInto(recipeId);

        await RunJobAsync(recipeId, uploaded.ImageId);

        var afterSecond = await GetImageAsync(uploaded.ImageId);
        afterSecond.MediumUrl.Should().Be(afterFirst.MediumUrl);
        afterSecond.ThumbnailUrl.Should().Be(afterFirst.ThumbnailUrl);
        UploadsInto(recipeId).Should().Be(uploadsAfterFirst);
    }

    [Fact(DisplayName = "FR-JOB-002/D22/D44: ảnh đã bị xóa trước khi job chạy → no-op, không lỗi, không upload")]
    public async Task Job_ImageDeleted_IsNoOp()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(userId);
        var uploaded = await UploadAsync(recipeId, TestImages.Png(640, 480), "image/png", token);

        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/recipes/{recipeId}/images/{uploaded.ImageId}");
        delete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await _client.SendAsync(delete)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var uploadsBefore = UploadsInto(recipeId);

        var act = () => RunJobAsync(recipeId, uploaded.ImageId);

        await act.Should().NotThrowAsync();
        UploadsInto(recipeId).Should().Be(uploadsBefore);
    }

    [Fact(DisplayName = "FR-JOB-002/D1/D44: recipe đã soft delete trước khi job chạy → no-op, URL giữ null")]
    public async Task Job_RecipeSoftDeleted_IsNoOp()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(userId);
        var uploaded = await UploadAsync(recipeId, TestImages.Png(640, 480), "image/png", token);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
            var recipe = await db.Recipes.SingleAsync(r => r.Id == recipeId);
            recipe.SoftDelete();
            await db.SaveChangesAsync();
        }

        var act = () => RunJobAsync(recipeId, uploaded.ImageId);

        await act.Should().NotThrowAsync();
        var image = await GetImageAsync(uploaded.ImageId);
        image.MediumUrl.Should().BeNull();
        image.ThumbnailUrl.Should().BeNull();
    }

    [Fact(DisplayName = "FR-JOB-002/D41: ảnh gốc AVIF → no-op (Skia không decode được), URL giữ null, không ném lỗi")]
    public async Task Job_AvifOriginal_IsNoOp()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(userId);
        var uploaded = await UploadAsync(recipeId, TestImages.AvifHeaderOnly(), "image/avif", token);
        var uploadsBefore = UploadsInto(recipeId);

        var act = () => RunJobAsync(recipeId, uploaded.ImageId);

        await act.Should().NotThrowAsync();
        var image = await GetImageAsync(uploaded.ImageId);
        image.MediumUrl.Should().BeNull();
        image.ThumbnailUrl.Should().BeNull();
        UploadsInto(recipeId).Should().Be(uploadsBefore);
    }

    [Fact(DisplayName = "FR-JOB-002/D42: ResizeRecipeImageJob retry 3 lần, chờ 1/5/30 phút, hết lượt → Failed")]
    public void Job_HasRetryPolicyFromD42()
    {
        var attribute = typeof(ResizeRecipeImageJob)
            .GetCustomAttributes(typeof(Hangfire.AutomaticRetryAttribute), inherit: false)
            .Cast<Hangfire.AutomaticRetryAttribute>()
            .Should().ContainSingle().Subject;

        attribute.Attempts.Should().Be(3);
        attribute.DelaysInSeconds.Should().Equal(60, 300, 1800);
        attribute.OnAttemptsExceeded.Should().Be(Hangfire.AttemptsExceededAction.Fail);
    }

    // ---------------- Helpers ----------------

    /// <summary>Chạy đúng đường Hangfire sẽ chạy: resolve job từ DI rồi gọi ExecuteAsync (job → MediatR command).</summary>
    private async Task RunJobAsync(Guid recipeId, Guid imageId)
    {
        using var scope = factory.Services.CreateScope();
        var job = ActivatorUtilities.CreateInstance<ResizeRecipeImageJob>(scope.ServiceProvider);
        await job.ExecuteAsync(recipeId, imageId, CancellationToken.None);
    }

    private int UploadsInto(Guid recipeId) =>
        factory.FileStorage.Uploads.Count(u => u.Folder == $"recipes/{recipeId}");

    private async Task<UploadImageResponse> UploadAsync(Guid recipeId, byte[] bytes, string contentType, string token)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", "anh");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/recipes/{recipeId}/images") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<UploadImageResponse>(JsonOptions))!;
    }

    private async Task<RecipeImage> GetImageAsync(Guid imageId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        return await db.RecipeImages.AsNoTracking().IgnoreQueryFilters().SingleAsync(i => i.Id == imageId);
    }

    private async Task<(string Token, string UserId)> RegisterAuthorAsync()
    {
        var email = $"author-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, password = "Str0ng!Pass1", displayName = "Tác giả test" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        return (body!.AccessToken, body.User.Id);
    }

    /// <summary>Seed Category + Recipe trực tiếp qua DbContext — giống ImagesTests (FR-RCP-003 chưa hiện thực).</summary>
    private async Task<Guid> SeedRecipeAsync(string authorId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();

        var suffix = Guid.NewGuid().ToString("N");
        var category = Category.Create($"Món test {suffix}", $"mon-test-{suffix}", null, null, 0);
        db.Categories.Add(category);

        var recipe = Recipe.Create(
            title: "Công thức test",
            slug: $"cong-thuc-test-{Guid.NewGuid():N}",
            description: "Mô tả test",
            prepTime: 10,
            cookTime: 20,
            servings: 2,
            difficulty: RecipeDifficulty.Easy,
            categoryId: category.Id,
            authorId: authorId);

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        return recipe.Id;
    }
}
