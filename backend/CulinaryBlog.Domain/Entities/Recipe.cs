using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Enums;

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
    /// Factory method linh hoạt để thỏa mãn mọi bài test tạo Recipe (hỗ trợ param tùy biến)
    /// </summary>
/// <summary>
    /// Factory method chuẩn hỗ trợ cả truyền tham số vị trí lẫn named parameters (title, categoryId, authorId, status, ...)
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
    

    /// <summary>
    /// Xuất bản công thức công khai (Quyết định D2: chỉ công thức Published mới hiển thị cho Guest).
    /// </summary>
    public void Publish()
    {
        Status = RecipeStatus.Published;
        PublishedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Đưa công thức vào trạng thái lưu trữ / ẩn.
    /// </summary>
    public void Archive()
    {
        Status = RecipeStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Chuyển lại về trạng thái bản nháp để chỉnh sửa.
    /// </summary>
    public void MoveToDraft()
    {
        Status = RecipeStatus.Draft;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Cập nhật các thông số nấu nướng cơ bản.
    /// </summary>
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
    /// Thêm ảnh món ăn vào bộ sưu tập.
    /// </summary>
    public void AddImage(string originalUrl, bool isPrimary = false, int displayOrder = 0)
    {
        if (isPrimary)
        {
            DemoteOtherPrimaryImages();
        }

        Images.Add(new RecipeImage
        {
            RecipeId = this.Id,
            OriginalUrl = originalUrl,
            IsPrimary = isPrimary,
            DisplayOrder = displayOrder,
            OrderIndex = displayOrder
        });
    }

    /// <summary>
    /// Thêm một bước hướng dẫn nấu ăn.
    /// </summary>
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

    /// <summary>
    /// Thêm một nguyên liệu cần chuẩn bị.
    /// </summary>
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

    /// <summary>
    /// Thiết lập thông tin dinh dưỡng.
    /// </summary>
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

    /// <summary>
    /// Xóa mềm công thức.
    /// </summary>
    public void Delete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public RecipeImage? RemoveImage(Guid imageId)
    {
        var img = Images.FirstOrDefault(x => x.Id == imageId);
        if (img != null)
        {
            Images.Remove(img);
        }
        return img;
    }

    public RecipeImage AttachImage(RecipeImage image)
    {
        if (image.IsPrimary)
        {
            DemoteOtherPrimaryImages(image.Id);
        }
        Images.Add(image);
        return image;
    }

    public RecipeImage AttachImage(string originalUrl, bool isPrimary = false, int displayOrder = 0)
    {
        var image = new RecipeImage
        {
            RecipeId = Id,
            OriginalUrl = originalUrl,
            IsPrimary = isPrimary,
            DisplayOrder = displayOrder,
            OrderIndex = displayOrder
        };
        return AttachImage(image);
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

        var img = new RecipeImage
        {
            RecipeId = Id,
            OriginalUrl = url,
            IsPrimary = isPrimary,
            DisplayOrder = order,
            OrderIndex = order
        };
        return AttachImage(img);
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

    public RecipeImage? UpdateImage(Guid imageId, bool isPrimary, int displayOrder)
    {
        var img = Images.FirstOrDefault(x => x.Id == imageId);
        if (img != null)
        {
            img.IsPrimary = isPrimary;
            img.DisplayOrder = displayOrder;
            img.OrderIndex = displayOrder;
            if (isPrimary)
            {
                DemoteOtherPrimaryImages(imageId);
            }
        }
        return img;
    }

   public RecipeImage? UpdateImage(Guid imageId, object? arg2, object? arg3 = null, object? arg4 = null)
    {
        var img = Images.FirstOrDefault(x => x.Id == imageId);
        if (img != null)
        {
            if (arg2 is bool b)
            {
                img.IsPrimary = b;
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

            if (img.IsPrimary)
            {
                DemoteOtherPrimaryImages(imageId);
            }
        }

        return img;
    }
}