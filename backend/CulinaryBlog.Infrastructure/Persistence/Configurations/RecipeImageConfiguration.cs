using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

/// <summary>SRS 7.5 + D22/D23. Quan hệ với Recipe cấu hình ở RecipeConfiguration.</summary>
public sealed class RecipeImageConfiguration : IEntityTypeConfiguration<RecipeImage>
{
    public void Configure(EntityTypeBuilder<RecipeImage> builder)
    {
        builder.ToTable("RecipeImages");

        // DisplayOrder chỉ là alias C# của OrderIndex, không phải cột.
        builder.Ignore(x => x.DisplayOrder);

        builder.Property(x => x.OriginalUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.MediumUrl).HasMaxLength(500);
        builder.Property(x => x.ThumbnailUrl).HasMaxLength(500);
        builder.Property(x => x.AltText).HasMaxLength(200);
        builder.Property(x => x.IsPrimary).HasDefaultValue(false);
        builder.Property(x => x.OrderIndex).HasDefaultValue(0);

        builder.HasIndex(x => x.RecipeId, "IX_RecipeImages_RecipeId");

        // D22 — mỗi recipe tối đa một ảnh Primary (migration AddRecipeImagePrimaryIndex).
        builder.HasIndex(x => x.RecipeId, "IX_RecipeImages_RecipeId_IsPrimary")
            .IsUnique()
            .HasFilter("\"IsPrimary\" = true");
    }
}
