using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

/// <summary>SRS 7.4 — nguyên liệu của Recipe.</summary>
public class RecipeIngredient : BaseEntity
{
    public Guid RecipeId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Số lượng (vd. 500). Null cho "vừa đủ".</summary>
    public decimal? Quantity { get; set; }

    /// <summary>Đơn vị (gram, ml, thìa canh, quả...).</summary>
    public string? Unit { get; set; }

    /// <summary>Ghi chú tùy chọn (vd. "thái lát mỏng").</summary>
    public string? Notes { get; set; }

    /// <summary>Thứ tự hiển thị trong danh sách nguyên liệu.</summary>
    public int OrderIndex { get; set; }

    public Recipe? Recipe { get; set; }

    public void Update(string name, decimal? quantity, string? unit, string? notes)
    {
        Name = name;
        Quantity = quantity;
        Unit = unit;
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }
}
