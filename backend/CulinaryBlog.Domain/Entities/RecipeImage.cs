using System.ComponentModel.DataAnnotations.Schema;
using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

public class RecipeImage : BaseEntity
{
    public Guid RecipeId { get; set; }
    public string OriginalUrl { get; set; } = string.Empty;
    
    // Thêm thuộc tính này:
    public string? AltText { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public int OrderIndex { get; set; }

    // [NotMapped]: Bí danh giúp tương thích với Recipe.cs mà KHÔNG sinh cột vào Database
    [NotMapped]
    public int DisplayOrder 
    { 
        get => OrderIndex; 
        set => OrderIndex = value; 
    }

    public void UnsetPrimary()
    {
        IsPrimary = false;
    }
    // DÒNG NÀY ĐỂ EF CORE BIẾT RÕ KHÓA NGOẠI LÀ RecipeId, KHÔNG TỰ SINH RecipeId1:
    [ForeignKey(nameof(RecipeId))]
    public Recipe? Recipe { get; set; }

    public void Update(string altText, int orderIndex)
    {
        // Bảo đảm AltText được cập nhật chính xác, không bị gán đè chuỗi rỗng
        AltText = altText ?? string.Empty;
        OrderIndex = orderIndex;
    }

    internal void SetPrimary(bool isPrimary)
    {
        IsPrimary = isPrimary;
    }
}