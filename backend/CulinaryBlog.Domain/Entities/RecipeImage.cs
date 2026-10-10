using System.ComponentModel.DataAnnotations.Schema;
using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

/// <summary>
/// Entity con của Recipe aggregate (FR-RCP-008/D22/D23). Mọi thay đổi PHẢI đi qua Recipe
/// (AttachImage/UpdateImage/RemoveImage) — setter chỉ internal để Recipe trong cùng assembly
/// Domain mới gán được, tầng ngoài (Application/Infrastructure) không được bypass.
/// </summary>
public class RecipeImage : BaseEntity
{
    public Guid RecipeId { get; internal set; }
    public string OriginalUrl { get; internal set; } = string.Empty;

    /// <summary>SRS 7.5 — ảnh 800×600 do FR-JOB-002 sinh. Null khi job chưa chạy.</summary>
    public string? MediumUrl { get; internal set; }

    /// <summary>SRS 7.5 — ảnh 300×300 do FR-JOB-002 sinh. Null khi job chưa chạy.</summary>
    public string? ThumbnailUrl { get; internal set; }

    public string? AltText { get; internal set; } = string.Empty;
    public bool IsPrimary { get; internal set; }
    public int OrderIndex { get; internal set; }

    // [NotMapped]: Bí danh giúp tương thích với code cũ mà KHÔNG sinh cột vào Database
    [NotMapped]
    public int DisplayOrder
    {
        get => OrderIndex;
        internal set => OrderIndex = value;
    }

    // DÒNG NÀY ĐỂ EF CORE BIẾT RÕ KHÓA NGOẠI LÀ RecipeId, KHÔNG TỰ SINH RecipeId1:
    [ForeignKey(nameof(RecipeId))]
    public Recipe? Recipe { get; internal set; }

    internal void UpdateAltText(string? altText) => AltText = altText ?? string.Empty;

    internal void UpdateOrderIndex(int orderIndex) => OrderIndex = orderIndex;

    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;

    internal void SetVariants(string mediumUrl, string thumbnailUrl)
    {
        MediumUrl = mediumUrl;
        ThumbnailUrl = thumbnailUrl;
    }
}
