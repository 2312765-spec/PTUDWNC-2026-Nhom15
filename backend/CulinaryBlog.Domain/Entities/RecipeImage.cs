using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

public class RecipeImage : BaseEntity
{
    public Guid RecipeId { get; set; }
    public string OriginalUrl { get; set; } = string.Empty;
    
    // Thêm thuộc tính này:
    public string? AltText { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
    public int OrderIndex { get; set; }
    public void UnsetPrimary()
    {
        IsPrimary = false;
    }

    public Recipe? Recipe { get; set; }
}