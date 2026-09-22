using Bogus;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Seeding;

/// <summary>
/// Sprint 0 (Tuần 1 — B): seed dữ liệu mẫu cho môi trường Development —
/// 5 author, ~8 category, 50 recipe rải đều Draft/Published/Archived.
///
/// Dùng slugify nội bộ, KHÔNG phải ISlugHelper thật (đó là việc S3 — B).
/// Mỗi recipe có sẵn >=1 step và >=1 ingredient để dữ liệu mẫu hợp lệ với D3
/// dù entity chưa có method Publish() (S5-S7 — C).
/// </summary>
public static class DbSeeder
{
    private static readonly (string Name, string Description)[] CategorySeeds =
    [
        ("Món khai vị", "Các món nhẹ nhàng mở đầu bữa ăn."),
        ("Món chính", "Món ăn trọng tâm của bữa cơm."),
        ("Món tráng miệng", "Đồ ngọt kết thúc bữa ăn."),
        ("Món chay", "Công thức không dùng thịt, cá."),
        ("Món nước", "Phở, bún, mì và các món có nước dùng."),
        ("Món nướng", "Các món chế biến bằng nhiệt trực tiếp."),
        ("Đồ uống", "Nước ép, sinh tố, trà."),
        ("Ăn vặt", "Món nhẹ dùng giữa các bữa chính."),
    ];

    public static async Task SeedAsync(CulinaryBlogDbContext db, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
        {
            return; // đã seed trước đó — idempotent.
        }

        var usedSlugs = new HashSet<string>(StringComparer.Ordinal);

        var authors = SeedAuthors();
        db.Users.AddRange(authors);

        var categories = CategorySeeds
            .Select(c => Category.Create(c.Name, UniqueSlug(c.Name, usedSlugs), c.Description, imageUrl: null, orderIndex: 0))
            .ToList();
        db.Categories.AddRange(categories);

        var recipes = SeedRecipes(authors, categories, usedSlugs);
        db.Recipes.AddRange(recipes);

        await db.SaveChangesAsync(ct);
    }

    private static List<ApplicationUser> SeedAuthors()
    {
        var hasher = new PasswordHasher<ApplicationUser>();
        var faker = new Faker("vi");

        var authors = new List<ApplicationUser>();
        for (var i = 0; i < 5; i++)
        {
            var displayName = faker.Name.FullName();
            var user = new ApplicationUser
            {
                UserName = faker.Internet.Email(displayName).ToLowerInvariant(),
                DisplayName = displayName,
                Bio = faker.Lorem.Sentence(12),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = true,
            };
            user.Email = user.UserName;
            user.NormalizedEmail = user.Email.ToUpperInvariant();
            user.NormalizedUserName = user.UserName.ToUpperInvariant();
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            // Mật khẩu dev — KHÔNG dùng ngoài môi trường Development.
            user.PasswordHash = hasher.HashPassword(user, "Password123!");

            authors.Add(user);
        }

        return authors;
    }

    private static List<Recipe> SeedRecipes(
        IReadOnlyList<ApplicationUser> authors, IReadOnlyList<Category> categories, HashSet<string> usedSlugs)
    {
        var faker = new Faker("vi");
        var recipes = new List<Recipe>();

        for (var i = 0; i < 50; i++)
        {
            var title = faker.Commerce.ProductName();
            // Rải đều 3 trạng thái để test được filter Draft/Published/Archived theo owner (B).
            var status = (RecipeStatus)(i % 3);
            // Npgsql chỉ chấp nhận DateTime.Kind=Utc cho timestamptz.
            var publishedAt = status == RecipeStatus.Draft
                ? null
                : (DateTime?)DateTime.SpecifyKind(faker.Date.Past(1), DateTimeKind.Utc);

            var recipe = Recipe.Create(
                title: title,
                slug: UniqueSlug(title, usedSlugs),
                description: faker.Lorem.Sentence(20),
                prepTime: faker.Random.Int(5, 60),
                cookTime: faker.Random.Int(0, 90), // D19: 0 hợp lệ (món không cần nấu).
                servings: faker.Random.Int(1, 8),
                difficulty: faker.PickRandom<RecipeDifficulty>(),
                categoryId: categories[faker.Random.Int(0, categories.Count - 1)].Id,
                authorId: authors[faker.Random.Int(0, authors.Count - 1)].Id,
                status: status,
                publishedAt: publishedAt);

            var stepCount = faker.Random.Int(2, 5);
            for (var s = 0; s < stepCount; s++)
            {
                recipe.AddStep(
                    title: faker.Commerce.ProductAdjective(),
                    description: faker.Lorem.Sentence(15),
                    timerMinutes: faker.Random.Bool() ? faker.Random.Int(1, 30) : null);
            }

            var ingredientCount = faker.Random.Int(3, 8);
            for (var n = 0; n < ingredientCount; n++)
            {
                recipe.AddIngredient(
                    name: faker.Commerce.ProductMaterial(),
                    quantity: faker.Random.Bool() ? faker.Random.Decimal(1, 500) : null,
                    unit: faker.Random.Bool() ? faker.PickRandom("gram", "ml", "thìa canh", "quả", "củ") : null);
            }

            // D27: ảnh đầu tiên tự động primary — không cần (và giờ không còn) truyền isPrimary.
            recipe.AttachImage($"https://placehold.co/800x600?text={Uri.EscapeDataString(title)}");

            recipes.Add(recipe);
        }

        return recipes;
    }

    /// <summary>
    /// Slugify tối giản chỉ để seed có dữ liệu hợp lệ — KHÔNG phải ISlugHelper thật
    /// (bỏ dấu tiếng Việt đầy đủ + auto-suffix "đúng chuẩn" D10 là việc S3 — B).
    /// </summary>
    private static string UniqueSlug(string text, HashSet<string> usedSlugs)
    {
        var baseSlug = new string(text
                .ToLowerInvariant()
                .Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray())
            .Replace('đ', 'd')
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .Aggregate(string.Empty, (acc, c) => acc.EndsWith('-') && c == '-' ? acc : acc + c)
            .Trim('-');

        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "item";
        }

        var slug = baseSlug;
        var suffix = 2;
        while (!usedSlugs.Add(slug))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }
}
