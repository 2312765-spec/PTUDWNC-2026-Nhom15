using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Exceptions;
using System.ComponentModel.DataAnnotations.Schema;
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
    /// FR-RCP-008 / Quyết định D22, D23:
    /// - Ảnh đầu tiên thêm vào tự động là Primary, OrderIndex = 0.
    /// - Ảnh thứ hai trở đi không Primary, OrderIndex = Max(OrderIndex) + 1.
    /// </summary>
    public RecipeImage AddImage(string originalUrl, string? altText = null, bool? isPrimary = null, int? displayOrder = null)
{
    bool finalIsPrimary = isPrimary ?? (Images.Count == 0);
    int finalOrder = displayOrder ?? (Images.Count == 0 ? 0 : (Images.Max(i => i.OrderIndex) + 1));

    if (finalIsPrimary)
    {
        DemoteOtherPrimaryImages();
    }

    var image = new RecipeImage
    {
        RecipeId = this.Id,
        OriginalUrl = originalUrl,
        AltText = altText ?? string.Empty,
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
    /// Xóa ảnh (D22). Nếu ảnh bị xóa là Primary, tự động chọn ảnh còn lại có OrderIndex nhỏ nhất làm Primary mới.
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

    /// <summary>
    /// Gắn ảnh thực thể vào Recipe (D22, D23).
    /// </summary>
    public RecipeImage AttachImage(RecipeImage image)
    {
        image.RecipeId = this.Id;

        if (Images.Count == 0)
        {
            image.IsPrimary = true;
            if (image.OrderIndex == 0 && image.DisplayOrder == 0)
            {
                image.OrderIndex = 0;
                image.DisplayOrder = 0;
            }
        }
        else
        {
            if (image.IsPrimary)
            {
                DemoteOtherPrimaryImages(image.Id);
            }

            int maxOrder = Images.Max(i => i.OrderIndex);
            if (image.OrderIndex == 0 && image.DisplayOrder == 0)
            {
                image.OrderIndex = maxOrder + 1;
                image.DisplayOrder = maxOrder + 1;
            }
        }

        Images.Add(image);
        return image;
    }

   public RecipeImage AttachImage(string originalUrl, bool? isPrimary = null, int? displayOrder = null)
{
    return AddImage(originalUrl, isPrimary: isPrimary, displayOrder: displayOrder);
}

public RecipeImage AttachImage(object arg1, object? arg2 = null, object? arg3 = null, object? arg4 = null)
{
    if (arg1 is RecipeImage recipeImage)
    {
        return AttachImage(recipeImage);
    }

    var url = arg1?.ToString() ?? string.Empty;
    string? altText = null;
    bool? isPrimary = null;
    int? order = null;

    object?[] args = new[] { arg2, arg3, arg4 };

    foreach (var arg in args)
    {
        if (arg is string s)
        {
            altText = s;
        }
        else if (arg is bool b)
        {
            isPrimary = b;
        }
        else if (arg is int i)
        {
            order = i;
        }
    }

    return AddImage(url, altText: altText, isPrimary: isPrimary, displayOrder: order);
}

    public void UpdateImage(Guid imageId, string? altText, int orderIndex)
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

    public RecipeImage UpdateImage(Guid imageId, bool isPrimary, int displayOrder)
    {
        var img = Images.FirstOrDefault(x => x.Id == imageId);
        if (img == null)
        {
            throw new DomainException("Không tìm thấy ảnh để cập nhật.", "IMAGE_NOT_FOUND");
        }

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

        object?[] args = new[] { arg2, arg3, arg4 };

        foreach (var arg in args)
        {
            if (arg is string s)
            {
                img.AltText = s;
            }
            else if (arg is bool b)
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
            else if (arg is int i)
            {
                img.DisplayOrder = i;
                img.OrderIndex = i;
            }
        }

        return img;
    }

    public void UpdateImageAltTextAndOrderIndex(Guid imageId, string? altText, int orderIndex)
    {
        UpdateImage(imageId, altText, orderIndex);
    }
}