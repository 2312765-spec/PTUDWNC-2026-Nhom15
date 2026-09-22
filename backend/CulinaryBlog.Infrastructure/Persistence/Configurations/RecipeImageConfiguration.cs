using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

/// <summary>SRS 7.5.</summary>
public sealed class RecipeImageConfiguration : IEntityTypeConfiguration<RecipeImage>
{
    public void Configure(EntityTypeBuilder<RecipeImage> builder)
    {
        builder.ToTable("RecipeImages");

        builder.Property(i => i.OriginalUrl).HasMaxLength(500).IsRequired();
        builder.Property(i => i.MediumUrl).HasMaxLength(500);
        builder.Property(i => i.ThumbnailUrl).HasMaxLength(500);
        builder.Property(i => i.AltText).HasMaxLength(200);
        builder.Property(i => i.IsPrimary).HasDefaultValue(false);
        builder.Property(i => i.OrderIndex).HasDefaultValue(0);

        // Đặt tên tường minh ngay tại HasIndex() (không phải .HasDatabaseName() sau) — đây là
        // cách EF Core phân biệt HAI index khác nhau trên CÙNG một property, nếu không sẽ bị
        // coi là cùng một index và cái thêm sau ghi đè cái trước (phát hiện lúc tạo migration).
        builder.HasIndex(i => i.RecipeId, "IX_RecipeImages_RecipeId");

        // D27: bảo đảm "chỉ 1 ảnh IsPrimary=true / Recipe" ở TẦNG DB — lớp phòng thủ thứ hai,
        // độc lập với business rule ở Recipe.UpdateImage/AttachImage/RemoveImage (Domain).
        builder.HasIndex(i => i.RecipeId, "IX_RecipeImages_RecipeId_IsPrimary")
            .IsUnique()
            .HasFilter("\"IsPrimary\" = true");
    }
}
