using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Exceptions;

namespace CulinaryBlog.Domain.Entities;

public class Recipe : BaseEntity, IAggregateRoot
{
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    /// <summary>SRS 7.2 (legacy) + D18 — hướng dẫn tổng quan dạng markdown, nullable. Chi tiết dùng Steps.</summary>
    public string? Instructions { get; private set; }

    public int PrepTime { get; private set; }
    public int CookTime { get; private set; }
    public int Servings { get; private set; } = 4;
    public RecipeDifficulty Difficulty { get; private set; } = RecipeDifficulty.Easy;
    public RecipeStatus Status { get; private set; } = RecipeStatus.Draft;
    public string AuthorId { get; private set; } = string.Empty;
    public DateTime? PublishedAt { get; private set; }

    public Guid CategoryId { get; private set; }
    public Category? Category { get; private set; }

    private readonly List<RecipeImage> _images = [];
    public IReadOnlyCollection<RecipeImage> Images => _images.AsReadOnly();

    private readonly List<RecipeIngredient> _ingredients = [];
    public IReadOnlyCollection<RecipeIngredient> Ingredients => _ingredients.AsReadOnly();

    private readonly List<RecipeStep> _steps = [];
    public IReadOnlyCollection<RecipeStep> Steps => _steps.AsReadOnly();

    /// <summary>SRS 7.2.1 — owned, luôn có instance (các giá trị bên trong có thể null hết).</summary>
    public RecipeNutrition Nutrition { get; private set; } = new();

    private Recipe() { }

    private Recipe(
        string title,
        string slug,
        string description,
        int prepTime,
        int cookTime,
        int servings,
        RecipeDifficulty difficulty,
        string authorId,
        Guid categoryId)
    {
        Title = title;
        Slug = slug;
        Description = description;
        PrepTime = prepTime;
        CookTime = cookTime;
        Servings = servings;
        Difficulty = difficulty;
        AuthorId = authorId;
        CategoryId = categoryId;
        Status = RecipeStatus.Draft;
    }

    public static Recipe Create(
        string title = "Test Recipe",
        string slug = "test-recipe",
        string description = "Test Description",
        int prepTime = 15,
        int cookTime = 30,
        int servings = 4,
        RecipeDifficulty difficulty = RecipeDifficulty.Easy,
        Guid? categoryId = null,
        string authorId = "test-author",
        RecipeStatus status = RecipeStatus.Draft)
    {
        var recipe = new Recipe(
            title,
            slug,
            description,
            prepTime,
            cookTime,
            servings,
            difficulty,
            authorId,
            categoryId ?? Guid.NewGuid());

        recipe.Status = status;
        if (status == RecipeStatus.Published)
        {
            recipe.PublishedAt = DateTime.UtcNow;
        }

        return recipe;
    }

    /// <summary>D3: Chỉ publish được khi có ít nhất 1 step VÀ ít nhất 1 ingredient.</summary>
    public void Publish()
    {
        if (_steps.Count == 0 || _ingredients.Count == 0)
        {
            throw new DomainException(
                "Không thể xuất bản công thức khi thiếu bước thực hiện hoặc nguyên liệu.",
                "RECIPE_PUBLISH_INCOMPLETE");
        }

        Status = RecipeStatus.Published;
        PublishedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = RecipeStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MoveToDraft()
    {
        Status = RecipeStatus.Draft;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(
        string title,
        string description,
        int prepTime,
        int cookTime,
        int servings,
        RecipeDifficulty difficulty,
        Guid categoryId)
    {
        Title = title;
        Description = description;
        PrepTime = prepTime;
        CookTime = cookTime;
        Servings = servings;
        Difficulty = difficulty;
        CategoryId = categoryId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DemoteOtherPrimaryImages(Guid? currentImageId = null)
    {
        foreach (var img in _images)
        {
            if (currentImageId == null || img.Id != currentImageId)
            {
                img.SetPrimary(false);
            }
        }
    }

    /// <summary>
    /// FR-RCP-008 / Quyết định D22, D23:
    /// - Ảnh đầu tiên thêm vào tự động là Primary, OrderIndex = 0.
    /// - Ảnh thứ hai trở đi không Primary, OrderIndex = Max(OrderIndex) + 1.
    /// </summary>
    public RecipeImage AttachImage(string originalUrl, string? altText = null, bool? isPrimary = null, int? displayOrder = null)
    {
        bool finalIsPrimary = isPrimary ?? (_images.Count == 0);
        int finalOrder = displayOrder ?? (_images.Count == 0 ? 0 : (_images.Max(i => i.OrderIndex) + 1));

        if (finalIsPrimary)
        {
            DemoteOtherPrimaryImages();
        }

        var image = new RecipeImage
        {
            RecipeId = Id,
            OriginalUrl = originalUrl,
            AltText = altText ?? string.Empty,
            IsPrimary = finalIsPrimary,
            OrderIndex = finalOrder,
        };

        _images.Add(image);
        return image;
    }

    public void AddStep(int stepNumber, string title, string description, int? timerMinutes = null, string? imageUrl = null)
    {
        _steps.Add(new RecipeStep
        {
            RecipeId = Id,
            StepNumber = stepNumber,
            Title = title,
            Description = description,
            TimerMinutes = timerMinutes,
            ImageUrl = imageUrl
        });
    }

    /// <summary>SRS 7.4 — nguyên liệu mới xếp cuối danh sách (OrderIndex = max + 1).</summary>
    public RecipeIngredient AddIngredient(string name, decimal? quantity, string? unit, string? notes = null)
    {
        var ingredient = new RecipeIngredient
        {
            RecipeId = Id,
            Name = name,
            Quantity = quantity,
            Unit = unit,
            Notes = notes,
            OrderIndex = _ingredients.Count == 0 ? 0 : _ingredients.Max(i => i.OrderIndex) + 1,
        };

        _ingredients.Add(ingredient);
        return ingredient;
    }

    /// <summary>SRS 7.2.1 — thay toàn bộ thông tin dinh dưỡng (giá trị trên 1 khẩu phần, null = không khai báo).</summary>
    public void SetNutrition(
        decimal? calories,
        decimal? protein,
        decimal? carbohydrates,
        decimal? fat,
        decimal? fiber = null,
        decimal? sodium = null)
    {
        Nutrition = new RecipeNutrition(calories, protein, carbohydrates, fat, fiber, sodium);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Xóa ảnh (D22). Nếu ảnh bị xóa là Primary, tự động chọn ảnh còn lại có OrderIndex nhỏ nhất làm Primary mới.
    /// </summary>
    public RecipeImage RemoveImage(Guid imageId)
    {
        var img = _images.FirstOrDefault(x => x.Id == imageId)
            ?? throw new DomainException("Không tìm thấy ảnh để xóa.", "RECIPE_IMAGE_NOT_FOUND");

        bool wasPrimary = img.IsPrimary;
        _images.Remove(img);

        if (wasPrimary && _images.Count > 0)
        {
            var nextPrimary = _images.OrderBy(i => i.OrderIndex).First();
            nextPrimary.SetPrimary(true);
        }

        return img;
    }

    /// <summary>
    /// Cập nhật ảnh (D22/D23). Cả 3 trường AltText/IsPrimary/OrderIndex đều tùy chọn (null = giữ nguyên)
    /// và được áp dụng trong CÙNG một lần gọi — tránh bug "chỉ gửi orderIndex thì không lưu gì".
    /// </summary>
    public RecipeImage UpdateImage(Guid imageId, string? altText = null, bool? isPrimary = null, int? orderIndex = null)
    {
        var img = _images.FirstOrDefault(x => x.Id == imageId)
            ?? throw new DomainException("Không tìm thấy ảnh để cập nhật.", "RECIPE_IMAGE_NOT_FOUND");

        if (isPrimary == false && img.IsPrimary)
        {
            throw new DomainException("Công thức luôn yêu cầu phải có một ảnh đại diện chính.", "RECIPE_PRIMARY_IMAGE_REQUIRED");
        }

        if (altText is not null)
        {
            img.UpdateAltText(altText);
        }

        if (orderIndex.HasValue)
        {
            img.UpdateOrderIndex(orderIndex.Value);
        }

        if (isPrimary == true)
        {
            img.SetPrimary(true);
            DemoteOtherPrimaryImages(imageId);
        }

        return img;
    }

    /// <summary>FR-JOB-002/D44 — gán URL ảnh medium/thumbnail do job resize sinh ra.</summary>
    public RecipeImage SetImageVariants(Guid imageId, string mediumUrl, string thumbnailUrl) =>
        throw new NotImplementedException();
}
