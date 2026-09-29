using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

/// <summary>
/// SRS 7.2 + 7.2.1. D18 (Instructions nullable) · D19 (CookTime >= 0, PrepTime/Servings > 0).
/// </summary>
public sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("Recipes", t =>
        {
            t.HasCheckConstraint("CK_Recipes_PrepTime", "\"PrepTime\" > 0");
            t.HasCheckConstraint("CK_Recipes_CookTime", "\"CookTime\" >= 0");
            t.HasCheckConstraint("CK_Recipes_Servings", "\"Servings\" > 0");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Slug).HasMaxLength(220).IsRequired();
        builder.Property(r => r.Description).IsRequired();
        builder.Property(r => r.AuthorId).HasMaxLength(450).IsRequired();

        builder.Property(r => r.Difficulty).HasConversion<short>();
        builder.Property(r => r.Status).HasConversion<short>();

        builder.HasIndex(r => r.Slug).IsUnique();
        builder.HasIndex(r => r.CategoryId);
        builder.HasIndex(r => r.AuthorId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.Difficulty);

        // QUAN TRỌNG: Bỏ qua Nutrition để EF Core KHÔNG tìm các cột Nutrition_* trong bảng Recipes
        builder.Ignore(r => r.Nutrition);

        // FK Category
        builder.HasOne(r => r.Category)
            .WithMany(c => c.Recipes)
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK Author
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK Steps
        builder.HasMany(r => r.Steps)
            .WithOne()
            .HasForeignKey(s => s.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK Ingredients
        builder.HasMany(r => r.Ingredients)
            .WithOne()
            .HasForeignKey(i => i.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK Images
        builder.HasMany(r => r.Images)
            .WithOne(i => i.Recipe)
            .HasForeignKey(i => i.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}