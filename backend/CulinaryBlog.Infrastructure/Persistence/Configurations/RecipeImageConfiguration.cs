using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public class RecipeImageConfiguration : IEntityTypeConfiguration<RecipeImage>
{
    public void Configure(EntityTypeBuilder<RecipeImage> builder)
    {
        builder.HasKey(x => x.Id);

        // Tạm thời comment các trường chưa có trong Domain Entity RecipeImage
        // builder.Property(x => x.MediumUrl);
        // builder.Property(x => x.ThumbnailUrl);
        // builder.Property(x => x.OrderIndex);
    }
}