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

        builder.HasIndex(i => i.RecipeId);
    }
}
