using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.IntegrationTests.Recipes.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Recipes;

/// <summary>
/// FR-RCP-008, FR-FILE-001/002 — SRS mục 3.3/8.4 + docs/decisions.md D1, D22, D27, D28, D30.
///
/// FR-RCP-003 (tạo recipe) chưa hiện thực (C, S7) nên recipe/category được seed thẳng qua
/// DbContext trong test này, không qua API — đúng tinh thần OWNER.md của
/// IntegrationTests/Recipes ("B + C + D" cùng dùng một chỗ, D chỉ seed đủ dữ liệu cho ảnh).
///
/// Trạng thái khi viết file này: 3 endpoint còn là NotImplementedResults.Pending (501) —
/// toàn bộ test dưới đây phải FAIL (đỏ) cho tới khi Domain/Application/Infrastructure/API
/// của FR-RCP-008 được hiện thực theo kế hoạch trong docs/traceability.md.
/// </summary>
public sealed class ImagesTests(RecipeImagesApiFactory factory) : IClassFixture<RecipeImagesApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    private sealed record UploadImageResponse(Guid ImageId, string OriginalUrl, string? AltText, bool IsPrimary);

    // ---------------- Upload ----------------

    [Fact(DisplayName = "FR-RCP-008/D27: chủ sở hữu upload ảnh hợp lệ → 201, ảnh đầu tiên tự động primary")]
    public async Task Upload_AsOwner_Returns201AndFirstImageIsPrimary()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(userId);

        var response = await _client.SendAsync(
            MultipartUploadRequest(recipeId, JpegBytes(), "image/jpeg", "pho.jpg", token, "Ảnh phở"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<UploadImageResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.IsPrimary.Should().BeTrue();
        body.AltText.Should().Be("Ảnh phở");
        body.OriginalUrl.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "permissions.md: Author khác không phải chủ sở hữu → 403 RECIPE_FORBIDDEN")]
    public async Task Upload_AsOtherAuthor_Returns403()
    {
        var (_, ownerId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(ownerId);
        var (otherToken, _) = await RegisterAuthorAsync();

        var response = await _client.SendAsync(
            MultipartUploadRequest(recipeId, JpegBytes(), "image/jpeg", "pho.jpg", otherToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.RecipeForbidden);
    }

    [Fact(DisplayName = "permissions.md: Admin upload ảnh cho recipe của Author khác → 201 (bypass ownership)")]
    public async Task Upload_AsAdmin_OnOthersRecipe_Returns201()
    {
        var (_, ownerId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(ownerId);
        var adminToken = await RegisterAdminAsync();

        var response = await _client.SendAsync(
            MultipartUploadRequest(recipeId, JpegBytes(), "image/jpeg", "pho.jpg", adminToken));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "permissions.md: chưa đăng nhập → 401")]
    public async Task Upload_NoAuth_Returns401()
    {
        var (_, ownerId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(ownerId);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(JpegBytes());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "pho.jpg");

        var response = await _client.PostAsync($"/api/v1/recipes/{recipeId}/images", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "FR-RCP-008: recipe không tồn tại → 404 RECIPE_NOT_FOUND")]
    public async Task Upload_RecipeNotFound_Returns404()
    {
        var (token, _) = await RegisterAuthorAsync();

        var response = await _client.SendAsync(
            MultipartUploadRequest(Guid.NewGuid(), JpegBytes(), "image/jpeg", "pho.jpg", token));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.RecipeNotFound);
    }

    [Fact(DisplayName = "CONS-007/A2/D30: file > 5MB → 400 FILE_SIZE_EXCEEDED (type đúng mã, không phải VALIDATION_ERROR)")]
    public async Task Upload_FileTooLarge_Returns400FileSizeExceeded()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(userId);

        var response = await _client.SendAsync(
            MultipartUploadRequest(recipeId, JpegBytes(6 * 1024 * 1024 + 1), "image/jpeg", "big.jpg", token));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.FileSizeExceeded);
    }

    [Fact(DisplayName = "D28/A3: .exe đổi tên .jpg, khai Content-Type image/jpeg → 400 FILE_MIME_INVALID (magic bytes bắt được)")]
    public async Task Upload_ExeRenamedToJpg_Returns400FileMimeInvalid()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(userId);

        var response = await _client.SendAsync(
            MultipartUploadRequest(recipeId, ExeBytes(), "image/jpeg", "virus.jpg", token));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.FileMimeInvalid);
    }

    // ---------------- Update (PATCH) ----------------

    [Fact(DisplayName = "D22: PATCH isPrimary=true cho ảnh thứ hai → ảnh đầu tự động về false")]
    public async Task Patch_SetSecondImagePrimary_FirstBecomesFalse()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(userId, recipe =>
        {
            recipe.AttachImage("https://fake/1.jpg");
            recipe.AttachImage("https://fake/2.jpg");
        });
        var (firstImageId, secondImageId) = await GetPrimaryAndSecondaryImageIdsAsync(recipeId);

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/recipes/{recipeId}/images/{secondImageId}")
        {
            Content = JsonContent.Create(new { isPrimary = true }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var images = await GetImagesAsync(recipeId);
        images.Single(i => i.Id == secondImageId).IsPrimary.Should().BeTrue();
        images.Single(i => i.Id == firstImageId).IsPrimary.Should().BeFalse();
    }

    [Fact(DisplayName = "FR-RCP-008/D22 (regression): đổi ảnh primary qua lại nhiều lần → luôn 200 và đúng 1 ảnh primary")]
    public async Task Patch_SwapPrimaryBackAndForth_AlwaysExactlyOnePrimary()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(userId, recipe =>
        {
            recipe.AttachImage("https://fake/1.jpg");
            recipe.AttachImage("https://fake/2.jpg");
            recipe.AttachImage("https://fake/3.jpg");
        });
        var imageIds = (await GetImagesAsync(recipeId)).Select(i => i.Id).ToList();

        // Đảo chiều (3 → 1 → 2 → 1 → 3): ảnh được nâng nằm TRƯỚC ảnh đang primary theo thứ tự nạp
        // thì trước đây EF nâng trước khi hạ → vi phạm unique index → 500.
        foreach (var target in new[] { imageIds[2], imageIds[0], imageIds[1], imageIds[0], imageIds[2] })
        {
            var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/recipes/{recipeId}/images/{target}")
            {
                Content = JsonContent.Create(new { isPrimary = true }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK, $"đặt ảnh {target} làm primary");
            var images = await GetImagesAsync(recipeId);
            images.Should().ContainSingle(i => i.IsPrimary).Which.Id.Should().Be(target);
        }
    }

    [Fact(DisplayName = "D22/D27: PATCH isPrimary=false trên ảnh đang primary → 400 RECIPE_PRIMARY_IMAGE_REQUIRED")]
    public async Task Patch_UnsetPrimaryOnOnlyImage_Returns400()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(userId, recipe => recipe.AttachImage("https://fake/1.jpg"));
        var imageId = (await GetImagesAsync(recipeId)).Single().Id;

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/recipes/{recipeId}/images/{imageId}")
        {
            Content = JsonContent.Create(new { isPrimary = false }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.RecipePrimaryImageRequired);
    }

    [Fact(DisplayName = "D27: PATCH không có field nào → 400 VALIDATION_ERROR")]
    public async Task Patch_EmptyBody_Returns400ValidationError()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(userId, recipe => recipe.AttachImage("https://fake/1.jpg"));
        var imageId = (await GetImagesAsync(recipeId)).Single().Id;

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/recipes/{recipeId}/images/{imageId}")
        {
            Content = JsonContent.Create(new { }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.ValidationError);
    }

    [Fact(DisplayName = "FR-RCP-008: PATCH ảnh không tồn tại → 404 RECIPE_IMAGE_NOT_FOUND")]
    public async Task Patch_ImageNotFound_Returns404()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(userId);

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/recipes/{recipeId}/images/{Guid.NewGuid()}")
        {
            Content = JsonContent.Create(new { altText = "x" }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Type.Should().Be(ErrorCodes.RecipeImageNotFound);
    }

    // ---------------- Delete ----------------

    [Fact(DisplayName = "D22: xóa ảnh đang primary, còn ảnh khác → ảnh orderIndex nhỏ nhất tự lên primary")]
    public async Task Delete_PrimaryImage_PromotesNextImage()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(userId, recipe =>
        {
            recipe.AttachImage("https://fake/1.jpg");
            recipe.AttachImage("https://fake/2.jpg");
        });
        var (firstImageId, secondImageId) = await GetPrimaryAndSecondaryImageIdsAsync(recipeId);

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/recipes/{recipeId}/images/{firstImageId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var images = await GetImagesAsync(recipeId);
        images.Should().ContainSingle(i => i.Id == secondImageId && i.IsPrimary);
    }

    [Fact(DisplayName = "D1: xóa ảnh không cascade, không xóa recipe — chỉ RecipeImage biến mất khỏi DB")]
    public async Task Delete_Image_RemovesOnlyThatRow()
    {
        var (token, userId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(userId, recipe => recipe.AttachImage("https://fake/1.jpg"));
        var imageId = (await GetImagesAsync(recipeId)).Single().Id;

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/recipes/{recipeId}/images/{imageId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        (await db.RecipeImages.FindAsync(imageId)).Should().BeNull();
        (await db.Recipes.FindAsync(recipeId)).Should().NotBeNull();
    }

    [Fact(DisplayName = "permissions.md: Author khác xóa ảnh → 403 RECIPE_FORBIDDEN")]
    public async Task Delete_AsOtherAuthor_Returns403()
    {
        var (_, ownerId) = await RegisterAuthorAsync();
        var recipeId = await SeedRecipeAsync(ownerId, recipe => recipe.AttachImage("https://fake/1.jpg"));
        var imageId = (await GetImagesAsync(recipeId)).Single().Id;
        var (otherToken, _) = await RegisterAuthorAsync();

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/recipes/{recipeId}/images/{imageId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------- Helpers ----------------

    private static byte[] JpegBytes(int totalSize = 64)
    {
        var bytes = new byte[Math.Max(totalSize, 4)];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        bytes[3] = 0xE0;
        return bytes;
    }

    private static byte[] ExeBytes() => [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    private static HttpRequestMessage MultipartUploadRequest(
        Guid recipeId, byte[] fileBytes, string contentType, string fileName, string token, string? altText = null)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        if (altText is not null)
        {
            content.Add(new StringContent(altText), "altText");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/recipes/{recipeId}/images") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
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

    /// <summary>
    /// D11: không có endpoint Admin quản lý user (ngoài scope v1) — gán role Admin thẳng qua
    /// UserManager rồi đăng nhập lại để lấy JWT có claim role mới (token cũ chỉ có Author),
    /// đúng cách LoginTests.SetIsActiveAsync đã thao tác DB trực tiếp cho tình huống tương tự.
    /// </summary>
    private async Task<string> RegisterAdminAsync()
    {
        var email = $"admin-{Guid.NewGuid():N}@example.com";
        const string password = "Str0ng!Pass1";
        var registerResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, password, displayName = "Admin test" });
        registerResponse.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            await userManager.AddToRoleAsync(user!, Roles.Admin);
        }

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();
        var body = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        return body!.AccessToken;
    }

    /// <summary>Seed Category + Recipe trực tiếp qua DbContext — FR-RCP-003 (Create) chưa hiện thực (C, S7).</summary>
    private async Task<Guid> SeedRecipeAsync(string authorId, Action<Recipe>? seedImages = null)
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

        seedImages?.Invoke(recipe);

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        return recipe.Id;
    }

    private async Task<List<RecipeImage>> GetImagesAsync(Guid recipeId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlogDbContext>();
        var recipe = await db.Recipes.Include(r => r.Images).AsNoTracking().SingleAsync(r => r.Id == recipeId);
        return [.. recipe.Images];
    }

    private async Task<(Guid PrimaryId, Guid SecondaryId)> GetPrimaryAndSecondaryImageIdsAsync(Guid recipeId)
    {
        var images = await GetImagesAsync(recipeId);
        return (images.First(i => i.IsPrimary).Id, images.First(i => !i.IsPrimary).Id);
    }
}
