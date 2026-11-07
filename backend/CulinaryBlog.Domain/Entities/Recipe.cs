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
        RecipeStatus status = RecipeStatus.Draft,
        string? instructions = null)
    {
        EnsureValidTimes(prepTime, cookTime, servings);

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

        recipe.Instructions = instructions;
        recipe.Status = status;
        if (status == RecipeStatus.Published)
        {
            recipe.PublishedAt = DateTime.UtcNow;
        }

        return recipe;
    }

    /// <summary>
    /// D19 + CHECK constraint SRS 7.2: prepTime &gt; 0, cookTime &gt;= 0 (0 = món không cần nấu), servings &gt; 0.
    /// Validator đã chặn từ trước (400) — đây là lớp bảo vệ cuối của aggregate.
    /// </summary>
    private static void EnsureValidTimes(int prepTime, int cookTime, int servings)
    {
        if (prepTime <= 0 || cookTime < 0 || servings <= 0)
        {
            throw new DomainException(
                "Thời gian chuẩn bị và khẩu phần phải lớn hơn 0, thời gian nấu không được âm.",
                "VALIDATION_ERROR");
        }
    }

    /// <summary>
    /// FR-RCP-005 — Draft/Archived → Published. D3: cần ít nhất 1 step VÀ ít nhất 1 ingredient.
    /// D77: đã Published → không đổi gì (idempotent, trả <c>false</c>); <see cref="PublishedAt"/> giữ thời điểm
    /// xuất bản LẦN ĐẦU, publish lại sau khi unpublish không đổi nó.
    /// </summary>
    /// <returns><c>true</c> nếu trạng thái thực sự đổi.</returns>
    public bool Publish()
    {
        if (Status == RecipeStatus.Published)
        {
            return false;
        }

        if (_steps.Count == 0 || _ingredients.Count == 0)
        {
            throw new DomainException(
                "Công thức phải có ít nhất 1 nguyên liệu và 1 bước thực hiện.",
                "RECIPE_PUBLISH_INCOMPLETE");
        }

        Status = RecipeStatus.Published;
        PublishedAt ??= DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// FR-RCP-005 — Published/Archived → Draft. D77: đã Draft → không đổi gì (idempotent, trả <c>false</c>).
    /// </summary>
    /// <returns><c>true</c> nếu trạng thái thực sự đổi.</returns>
    public bool Unpublish()
    {
        if (Status == RecipeStatus.Draft)
        {
            return false;
        }

        Status = RecipeStatus.Draft;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// FR-RCP-006 — Draft/Published → Archived (ẩn khỏi public, không xóa gì — D1). D79: đã Archived → không đổi gì
    /// (idempotent, trả <c>false</c>); <see cref="PublishedAt"/> giữ nguyên. Thoát khỏi Archived qua Publish/Unpublish (D77).
    /// </summary>
    /// <returns><c>true</c> nếu trạng thái thực sự đổi.</returns>
    public bool Archive()
    {
        if (Status == RecipeStatus.Archived)
        {
            return false;
        }

        Status = RecipeStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public void MoveToDraft()
    {
        Status = RecipeStatus.Draft;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// FR-RCP-004 — thay toàn bộ thông tin cơ bản (D69). KHÔNG đổi Slug (D13) và Status.
    /// Nutrition đổi riêng qua <see cref="SetNutrition"/>; steps/ingredients qua FR-RCP-009/010.
    /// </summary>
    public void Update(
        string title,
        string description,
        string? instructions,
        int prepTime,
        int cookTime,
        int servings,
        RecipeDifficulty difficulty,
        Guid categoryId)
    {
        EnsureValidTimes(prepTime, cookTime, servings);
        UpdateDetails(title, description, prepTime, cookTime, servings, difficulty, categoryId);
        Instructions = instructions;
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
        EnsureValidTimes(prepTime, cookTime, servings);
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

    /// <summary>
    /// FR-RCP-010/D6 — thêm bước vào cuối: StepNumber = Max + 1 (hoặc 1 nếu chưa có bước nào).
    /// Server sinh số thứ tự, client không gửi.
    /// </summary>
    public RecipeStep AppendStep(string title, string description, int? timerMinutes, string? imageUrl)
    {
        var step = new RecipeStep
        {
            RecipeId = Id,
            StepNumber = _steps.Count == 0 ? 1 : _steps.Max(s => s.StepNumber) + 1,
            Title = title,
            Description = description,
            TimerMinutes = timerMinutes,
            ImageUrl = imageUrl,
        };

        _steps.Add(step);
        return step;
    }

    /// <summary>
    /// FR-RCP-010/D6 — sửa nội dung bước; <paramref name="newPosition"/> (1..số bước) để đổi vị trí, null = giữ
    /// nguyên. Sau khi đổi vị trí, mọi bước được đánh số lại liên tục 1, 2, 3…
    /// </summary>
    public RecipeStep UpdateStep(Guid stepId, string title, string description, int? timerMinutes, string? imageUrl, int? newPosition)
    {
        var step = FindStep(stepId);

        if (newPosition is { } position && (position < 1 || position > _steps.Count))
        {
            throw new DomainException(
                $"Vị trí bước phải nằm trong khoảng 1 đến {_steps.Count}.",
                "VALIDATION_ERROR");
        }

        step.Update(title, description, timerMinutes, imageUrl);

        if (newPosition is { } target)
        {
            var ordered = _steps.OrderBy(s => s.StepNumber).ToList();
            ordered.Remove(step);
            ordered.Insert(target - 1, step);
            Renumber(ordered);
        }

        return step;
    }

    /// <summary>
    /// FR-RCP-010/D6 — bỏ bước khỏi recipe và đánh số lại các bước còn lại 1, 2, 3…
    /// D3/D73: recipe đang Published phải còn ít nhất 1 bước.
    /// </summary>
    public RecipeStep RemoveStep(Guid stepId)
    {
        var step = FindStep(stepId);

        if (Status == RecipeStatus.Published && _steps.Count == 1)
        {
            throw new DomainException(
                "Công thức đã xuất bản phải còn ít nhất 1 bước thực hiện.",
                "RECIPE_PUBLISH_INCOMPLETE");
        }

        _steps.Remove(step);
        Renumber(_steps.OrderBy(s => s.StepNumber).ToList());
        return step;
    }

    private RecipeStep FindStep(Guid stepId) =>
        _steps.FirstOrDefault(s => s.Id == stepId)
        ?? throw new DomainException("Không tìm thấy bước thực hiện.", "RECIPE_STEP_NOT_FOUND");

    /// <summary>Gán StepNumber 1..n theo đúng thứ tự danh sách; chỉ ghi khi số thay đổi.</summary>
    private static void Renumber(IReadOnlyList<RecipeStep> ordered)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].StepNumber != i + 1)
            {
                ordered[i].StepNumber = i + 1;
            }
        }
    }

    /// <summary>Chỉ dùng cho seed/test dựng dữ liệu có sẵn số thứ tự. Luồng nghiệp vụ dùng <see cref="AppendStep"/>.</summary>
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

    /// <summary>
    /// SRS 7.4 / FR-RCP-009 / D76 — <paramref name="position"/> null: xếp cuối danh sách (OrderIndex = max + 1).
    /// Có giá trị (0..số nguyên liệu): chèn vào vị trí đó, mọi nguyên liệu được đánh lại 0, 1, 2…
    /// </summary>
    public RecipeIngredient AddIngredient(string name, decimal? quantity, string? unit, string? notes = null, int? position = null)
    {
        if (position is { } p && (p < 0 || p > _ingredients.Count))
        {
            throw new DomainException(
                $"Vị trí nguyên liệu phải nằm trong khoảng 0 đến {_ingredients.Count}.",
                "VALIDATION_ERROR");
        }

        var ingredient = new RecipeIngredient
        {
            RecipeId = Id,
            Name = name,
            Quantity = quantity,
            Unit = unit,
            Notes = notes,
            OrderIndex = _ingredients.Count == 0 ? 0 : _ingredients.Max(i => i.OrderIndex) + 1,
        };

        if (position is { } target)
        {
            var ordered = OrderedIngredients();
            ordered.Insert(target, ingredient);
            ReindexIngredients(ordered);
        }

        _ingredients.Add(ingredient);
        return ingredient;
    }

    /// <summary>
    /// FR-RCP-009/D76 — thay nội dung nguyên liệu (quantity/unit/notes null = xóa). <paramref name="newPosition"/>
    /// (0..số nguyên liệu - 1) để đổi vị trí, null = giữ nguyên; sau khi đổi, đánh lại OrderIndex 0, 1, 2…
    /// </summary>
    public RecipeIngredient UpdateIngredient(Guid ingredientId, string name, decimal? quantity, string? unit, string? notes, int? newPosition)
    {
        var ingredient = FindIngredient(ingredientId);

        if (newPosition is { } p && (p < 0 || p >= _ingredients.Count))
        {
            throw new DomainException(
                $"Vị trí nguyên liệu phải nằm trong khoảng 0 đến {_ingredients.Count - 1}.",
                "VALIDATION_ERROR");
        }

        ingredient.Update(name, quantity, unit, notes);

        if (newPosition is { } target)
        {
            var ordered = OrderedIngredients();
            ordered.Remove(ingredient);
            ordered.Insert(target, ingredient);
            ReindexIngredients(ordered);
        }

        return ingredient;
    }

    /// <summary>
    /// FR-RCP-009/D76 — bỏ nguyên liệu khỏi recipe, đánh lại OrderIndex các nguyên liệu còn lại.
    /// D3: recipe đang Published phải còn ít nhất 1 nguyên liệu.
    /// </summary>
    public RecipeIngredient RemoveIngredient(Guid ingredientId)
    {
        var ingredient = FindIngredient(ingredientId);

        if (Status == RecipeStatus.Published && _ingredients.Count == 1)
        {
            throw new DomainException(
                "Công thức đã xuất bản phải còn ít nhất 1 nguyên liệu.",
                "RECIPE_PUBLISH_INCOMPLETE");
        }

        _ingredients.Remove(ingredient);
        ReindexIngredients(OrderedIngredients());
        return ingredient;
    }

    private RecipeIngredient FindIngredient(Guid ingredientId) =>
        _ingredients.FirstOrDefault(i => i.Id == ingredientId)
        ?? throw new DomainException("Không tìm thấy nguyên liệu.", "RECIPE_INGREDIENT_NOT_FOUND");

    /// <summary>Thứ tự hiển thị hiện tại; dữ liệu cũ trùng OrderIndex thì nguyên liệu tạo trước đứng trước.</summary>
    private List<RecipeIngredient> OrderedIngredients() =>
        _ingredients.OrderBy(i => i.OrderIndex).ThenBy(i => i.CreatedAt).ToList();

    /// <summary>Gán OrderIndex 0..n-1 theo đúng thứ tự danh sách; chỉ ghi khi giá trị thay đổi.</summary>
    private static void ReindexIngredients(IReadOnlyList<RecipeIngredient> ordered)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].OrderIndex != i)
            {
                ordered[i].OrderIndex = i;
            }
        }
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
    public RecipeImage SetImageVariants(Guid imageId, string mediumUrl, string thumbnailUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediumUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbnailUrl);

        var img = _images.FirstOrDefault(x => x.Id == imageId)
            ?? throw new DomainException("Không tìm thấy ảnh để gán phiên bản resize.", "RECIPE_IMAGE_NOT_FOUND");

        img.SetVariants(mediumUrl, thumbnailUrl);
        return img;
    }
}
