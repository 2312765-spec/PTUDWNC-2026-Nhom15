using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Domain.Entities;

/// <summary>
/// SRS 7.2 — thực thể trung tâm. TODO(S5–S7 — C): method nghiệp vụ Publish()/Unpublish()/
/// Archive() (D3: cần >=1 step VÀ >=1 ingredient để publish) và renumber step (D6) CHƯA
/// hiện thực ở đây. Sprint 0 (B) chỉ tạo shape dữ liệu đủ cho migration + seed.
/// </summary>
public sealed class Recipe : BaseEntity
{
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    /// <summary>D18 — legacy field, nullable. Chi tiết dùng <see cref="Steps"/>.</summary>
    public string? Instructions { get; private set; }

    public int PrepTime { get; private set; }
    public int CookTime { get; private set; }
    public int Servings { get; private set; }
    public RecipeDifficulty Difficulty { get; private set; }
    public RecipeStatus Status { get; private set; }
    public Guid CategoryId { get; private set; }
    public string AuthorId { get; private set; } = string.Empty;
    public DateTime? PublishedAt { get; private set; }
    public RecipeNutrition Nutrition { get; private set; } = RecipeNutrition.Empty();

    private readonly List<RecipeStep> _steps = [];
    public IReadOnlyCollection<RecipeStep> Steps => _steps.AsReadOnly();

    private readonly List<RecipeIngredient> _ingredients = [];
    public IReadOnlyCollection<RecipeIngredient> Ingredients => _ingredients.AsReadOnly();

    private readonly List<RecipeImage> _images = [];
    public IReadOnlyCollection<RecipeImage> Images => _images.AsReadOnly();

    private Recipe()
    {
    }

    /// <summary>
    /// Dùng cho seeding Sprint 0. Validate auto-suffix slug (D10) và điều kiện publish
    /// (D3) là việc của FR-RCP-003/005 (Sprint 2/3 — C), chưa hiện thực ở đây — tham số
    /// <paramref name="status"/>/<paramref name="publishedAt"/> chỉ để seed dữ liệu mẫu
    /// có đủ Draft/Published/Archived.
    /// </summary>
    public static Recipe Create(
        string title,
        string slug,
        string description,
        int prepTime,
        int cookTime,
        int servings,
        RecipeDifficulty difficulty,
        Guid categoryId,
        string authorId,
        string? instructions = null,
        RecipeStatus status = RecipeStatus.Draft,
        DateTime? publishedAt = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("RECIPE_TITLE_REQUIRED", "Tiêu đề công thức không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainException("RECIPE_SLUG_REQUIRED", "Slug công thức không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("RECIPE_DESCRIPTION_REQUIRED", "Mô tả công thức không được để trống.");
        }

        // D19: prepTime > 0, cookTime >= 0 (0 = món không cần nấu), servings > 0.
        if (prepTime <= 0)
        {
            throw new DomainException("RECIPE_PREP_TIME_INVALID", "Thời gian chuẩn bị phải > 0.");
        }

        if (cookTime < 0)
        {
            throw new DomainException("RECIPE_COOK_TIME_INVALID", "Thời gian nấu phải >= 0.");
        }

        if (servings <= 0)
        {
            throw new DomainException("RECIPE_SERVINGS_INVALID", "Số khẩu phần phải > 0.");
        }

        if (string.IsNullOrWhiteSpace(authorId))
        {
            throw new DomainException("RECIPE_AUTHOR_REQUIRED", "Công thức phải có tác giả.");
        }

        return new Recipe
        {
            Title = title,
            Slug = slug,
            Description = description,
            Instructions = instructions,
            PrepTime = prepTime,
            CookTime = cookTime,
            Servings = servings,
            Difficulty = difficulty,
            Status = status,
            CategoryId = categoryId,
            AuthorId = authorId,
            PublishedAt = publishedAt,
            Nutrition = RecipeNutrition.Empty(),
        };
    }

    /// <summary>Dùng cho seeding — thêm bước trực tiếp, không renumber (D6 để lại cho S6/S10 — C).</summary>
    public void AddStep(string title, string description, int? timerMinutes = null, string? imageUrl = null)
    {
        var nextNumber = _steps.Count == 0 ? 1 : _steps.Max(s => s.StepNumber) + 1;
        _steps.Add(RecipeStep.Create(Id, nextNumber, title, description, timerMinutes, imageUrl));
    }

    /// <summary>Dùng cho seeding — thêm nguyên liệu trực tiếp.</summary>
    public void AddIngredient(string name, decimal? quantity = null, string? unit = null, string? notes = null)
    {
        var orderIndex = _ingredients.Count;
        _ingredients.Add(RecipeIngredient.Create(Id, name, quantity, unit, notes, orderIndex));
    }
    /// <summary>
    /// D22: Hạ ảnh chính hiện tại về false trước khi lưu DB để tránh vi phạm Unique Index trên PostgreSQL.
    /// </summary>
    public void DemoteCurrentPrimaryImage(Guid targetNewPrimaryId)
    {
        foreach (var other in _images.Where(i => i.Id != targetNewPrimaryId && i.IsPrimary))
        {
            other.SetPrimary(false);
        }
    }

    /// <summary>
    /// FR-RCP-008/D27 — ảnh đầu tiên tự động là primary, client không được chọn (D22).
    /// orderIndex = Max(orderIndex hiện có) + 1, ảnh đầu tiên = 0.
    /// </summary>
    public RecipeImage AttachImage(string originalUrl, string? altText = null)
    {
        var isPrimary = _images.Count == 0;
        var orderIndex = _images.Count == 0 ? 0 : _images.Max(i => i.OrderIndex) + 1;

        var image = RecipeImage.Create(Id, originalUrl, isPrimary, orderIndex, altText: altText);
        _images.Add(image);
        return image;
    }

    /// <summary>
    /// FR-RCP-008/D22 — PATCH metadata ảnh. Handler phải tự kiểm tra ảnh tồn tại trước (404
    /// RECIPE_IMAGE_NOT_FOUND) — đó không phải business rule nên không nằm ở đây.
    /// </summary>
    public void UpdateImage(Guid imageId, string? altText, bool? isPrimary, int? orderIndex)
    {
        var image = FindImageOrThrow(imageId);

        if (isPrimary == false && image.IsPrimary)
        {
            throw new DomainException(
                "RECIPE_PRIMARY_IMAGE_REQUIRED",
                "Công thức phải luôn có đúng một ảnh chính — không thể bỏ primary mà không chọn ảnh khác thay thế.");
        }

        if (isPrimary == true)
        {
            foreach (var other in _images.Where(i => i.Id != imageId))
            {
                other.SetPrimary(false);
            }

            image.SetPrimary(true);
        }

        image.UpdateMetadata(altText, orderIndex);
    }

    /// <summary>FR-RCP-008/D22 — xóa ảnh đang primary thì ảnh còn lại có orderIndex nhỏ nhất tự lên thay.</summary>
    public RecipeImage RemoveImage(Guid imageId)
    {
        var image = FindImageOrThrow(imageId);
        _images.Remove(image);

        if (image.IsPrimary && _images.Count > 0)
        {
            _images.OrderBy(i => i.OrderIndex).First().SetPrimary(true);
        }

        return image;
    }

    /// <summary>
    /// Không throw NotFoundException (404) — lớp đó thuộc Application. Nhánh này chỉ chạy nếu
    /// handler gọi sai (bỏ qua bước kiểm tra tồn tại) — coi là lỗi lập trình, không phải business rule.
    /// </summary>
    private RecipeImage FindImageOrThrow(Guid imageId) =>
        _images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new InvalidOperationException($"Recipe {Id} không có ảnh {imageId}.");
}
