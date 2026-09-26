using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Exceptions;

namespace CulinaryBlog.Domain.Entities;

public class Recipe : BaseEntity, IAggregateRoot
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PrepTime { get; set; }
    public int CookTime { get; set; }
    public int Servings { get; set; } = 4;
    public RecipeDifficulty Difficulty { get; set; } = RecipeDifficulty.Easy;
    public RecipeStatus Status { get; set; } = RecipeStatus.Draft;
    public string AuthorId { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    public ICollection<RecipeImage> Images { get; set; } = new List<RecipeImage>();
    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<RecipeStep> Steps { get; set; } = new List<RecipeStep>();
    public RecipeNutrition? Nutrition { get; set; }

    public Recipe() { }

    public Recipe(
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

    /// <summary>
    /// Factory method chuẩn hỗ trợ cả truyền tham số vị trí lẫn named parameters
    /// </summary>
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

    public void Publish()
    {
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

    /// <summary>
    /// FR-RCP-008 / Quyết định D22, D23:
    /// - Ảnh đầu tiên thêm vào tự động là Primary, OrderIndex = 0.
    /// - Ảnh thứ hai trở đi không Primary, OrderIndex = Max(OrderIndex) + 1.
    /// </summary>
    public RecipeImage AddImage(string originalUrl, bool? isPrimary = null, int? displayOrder = null)
    {
        int nextOrder = Images.Count == 0 ? 0 : (Images.Max(i => i.OrderIndex) + 1);
        int finalOrder = displayOrder ?? nextOrder;
        bool finalIsPrimary = isPrimary ?? (Images.Count == 0);

        if (finalIsPrimary)
        {
            DemoteOtherPrimaryImages();
        }

        var image = new RecipeImage
        {
            RecipeId = this.Id,
            OriginalUrl = originalUrl,
            IsPrimary = finalIsPrimary,
            DisplayOrder = finalOrder,
            OrderIndex = finalOrder
        };

        Images.Add(image);
        return image;
    }

    public void AddStep(int stepNumber, string title, string description, int? timerMinutes = null, string? imageUrl = null)
    {
        Steps.Add(new RecipeStep
        {
            RecipeId = this.Id,
            StepNumber = stepNumber,
            Title = title,
            Description = description,
            TimerMinutes = timerMinutes,
            ImageUrl = imageUrl
        });
    }

    public void AddIngredient(string name, string amount, string? unit, string? preparation = null)
    {
        Ingredients.Add(new RecipeIngredient
        {
            RecipeId = this.Id,
            Name = name,
            Amount = amount,
            Unit = unit,
            Preparation = preparation
        });
    }

    public void SetNutrition(int calories, int protein, int carbs, int fat)
    {
        Nutrition = new RecipeNutrition
        {
            RecipeId = this.Id,
            Calories = calories,
            Protein = protein,
            Carbs = carbs,
            Fat = fat
        };
    }

    public void Delete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Quyết định D22: Xóa ảnh. Nếu ảnh bị xóa là Primary, tự động chọn ảnh còn lại có OrderIndex nhỏ nhất làm Primary mới.
    /// </summary>
    public RecipeImage RemoveImage(Guid imageId)
    {
        var img = Images.FirstOrDefault(x => x.Id == imageId);
        if (img == null)
        {
            throw new DomainException("Không tìm thấy ảnh để xóa.", "IMAGE_NOT_FOUND");
        }

        bool wasPrimary = img.IsPrimary;
        Images.Remove(img);

        if (wasPrimary && Images.Count > 0)
        {
            var nextPrimary = Images.OrderBy(i => i.OrderIndex).First();
            nextPrimary.IsPrimary = true;
        }

        return img;
    }

    public RecipeImage AttachImage(RecipeImage image)
    {
        if (Images.Count == 0)
        {
            image.IsPrimary = true;
            image.OrderIndex = 0;
            image.DisplayOrder = 0;
        }
        else
        {
            if (image.OrderIndex == 0 && Images.Any(i => i.OrderIndex == 0))
            {
                image.OrderIndex = Images.Max(i => i.OrderIndex) + 1;
                image.DisplayOrder = image.OrderIndex;
            }

            if (image.IsPrimary)
            {
                DemoteOtherPrimaryImages(image.Id);
            }
        }

        Images.Add(image);
        return image;
    }

    public RecipeImage AttachImage(string originalUrl, bool isPrimary = false, int displayOrder = 0)
    {
        return AddImage(originalUrl, isPrimary, displayOrder);
    }

    public RecipeImage AttachImage(object arg1, object? arg2 = null, object? arg3 = null, object? arg4 = null)
    {
        if (arg1 is RecipeImage recipeImage)
        {
            return AttachImage(recipeImage);
        }

        var url = arg1?.ToString() ?? string.Empty;
        var isPrimary = false;
        var order = 0;

        if (arg2 is bool b2)
        {
            isPrimary = b2;
        }
        else if (arg2 is int i2)
        {
            order = i2;
        }

        if (arg3 is int i3)
        {
            order = i3;
        }
        else if (arg3 is bool b3)
        {
            isPrimary = b3;
        }

        return AddImage(url, isPrimary, order);
    }

    public void DemoteOtherPrimaryImages(Guid? currentImageId = null)
    {
        foreach (var img in Images)
        {
            if (currentImageId == null || img.Id != currentImageId)
            {
                img.IsPrimary = false;
            }
        }
    }

    /// <summary>
    /// Quyết định D22: Cập nhật Primary. Nếu isPrimary = true, ảnh này thành primary và các ảnh khác về false.
    /// Nếu isPrimary = false trên ảnh đang không primary thì không đổi gì.
    /// </summary>
/// <summary>
    /// FR-RCP-008 / Quyết định D22:
    /// - Không cho phép set isPrimary = false trên ảnh đang là Primary -> Ném ngoại lệ RECIPE_PRIMARY_IMAGE_REQUIRED.
    /// - Nếu set isPrimary = true -> biến ảnh này thành Primary và hạ các ảnh khác về false.
    /// - Nếu set isPrimary = false trên ảnh đang không phải Primary -> không thay đổi trạng thái primary.
    /// </summary>
    public RecipeImage UpdateImage(Guid imageId, bool isPrimary, int displayOrder)
    {
        var img = Images.FirstOrDefault(x => x.Id == imageId);
        if (img == null)
        {
            throw new DomainException("Không tìm thấy ảnh để cập nhật.", "IMAGE_NOT_FOUND");
        }

        // Nếu ảnh đang là Primary mà cố tình set về false -> Báo lỗi
        if (img.IsPrimary && !isPrimary)
        {
            throw new DomainException("Công thức luôn yêu cầu phải có một ảnh đại diện chính.", "RECIPE_PRIMARY_IMAGE_REQUIRED");
        }

        img.DisplayOrder = displayOrder;
        img.OrderIndex = displayOrder;

        if (isPrimary)
        {
            img.IsPrimary = true;
            DemoteOtherPrimaryImages(imageId);
        }

        return img;
    }

    public RecipeImage UpdateImage(Guid imageId, object? arg2, object? arg3 = null, object? arg4 = null)
    {
        var img = Images.FirstOrDefault(x => x.Id == imageId);
        if (img == null)
        {
            throw new DomainException("Không tìm thấy ảnh để cập nhật.", "IMAGE_NOT_FOUND");
        }

        if (arg2 is bool b)
        {
            if (img.IsPrimary && !b)
            {
                throw new DomainException("Công thức luôn yêu cầu phải có một ảnh đại diện chính.", "RECIPE_PRIMARY_IMAGE_REQUIRED");
            }

            if (b)
            {
                img.IsPrimary = true;
                DemoteOtherPrimaryImages(imageId);
            }
        }

        if (arg3 is int i)
        {
            img.DisplayOrder = i;
            img.OrderIndex = i;
        }
        else if (arg2 is int i2)
        {
            img.DisplayOrder = i2;
            img.OrderIndex = i2;
        }

        return img;
    }

    public void UpdateImageAltTextAndOrderIndex(Guid imageId, string? altText, int orderIndex)
    {
        var img = Images.FirstOrDefault(x => x.Id == imageId);
        if (img == null)
        {
            throw new DomainException("Không tìm thấy ảnh để cập nhật.", "IMAGE_NOT_FOUND");
        }

        img.AltText = altText;
        img.OrderIndex = orderIndex;
        img.DisplayOrder = orderIndex;
    }
}