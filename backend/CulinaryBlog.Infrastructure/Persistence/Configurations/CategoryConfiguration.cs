using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

/// <summary>SRS 7.6.</summary>
public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Slug).HasMaxLength(120).IsRequired();
        builder.Property(c => c.Description);
        builder.Property(c => c.ImageUrl).HasMaxLength(500);
        builder.Property(c => c.OrderIndex).HasDefaultValue(0);

        builder.HasIndex(c => c.Name).IsUnique();
        builder.HasIndex(c => c.Slug).IsUnique();

        builder.HasMany(c => c.Recipes)
            .WithOne()
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Restrict); // D2: không bao giờ thật sự kích hoạt (soft delete)
    }
}
