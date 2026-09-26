using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

/// <summary>
/// SRS 7.2 + 7.2.1. D18 (Instructions nullable) · D19 (CookTime >= 0, PrepTime/Servings > 0).
/// SearchVector (tsvector + GIN + trigger) cố tình CHƯA map — đó là việc S10 (full-text search).
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

        builder.Property(r => r.Title).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Slug).HasMaxLength(220).IsRequired();
        builder.Property(r => r.Description).IsRequired();
       // builder.Property(r => r.Instructions); // D18: nullable
        builder.Property(r => r.AuthorId).HasMaxLength(450).IsRequired();

        builder.Property(r => r.Difficulty).HasConversion<short>();
        builder.Property(r => r.Status).HasConversion<short>();

        builder.HasIndex(r => r.Slug).IsUnique();
        builder.HasIndex(r => r.CategoryId);
        builder.HasIndex(r => r.AuthorId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.Difficulty);
    
        builder.OwnsOne(r => r.Nutrition, nutrition =>
        {
            // nutrition.Property(n => n.Calories).HasColumnName("Nutrition_Calories").HasPrecision(8, 2);
            // nutrition.Property(n => n.Protein).HasColumnName("Nutrition_Protein").HasPrecision(8, 2);
            // nutrition.Property(n => n.Carbohydrates).HasColumnName("Nutrition_Carbohydrates").HasPrecision(8, 2);
            // nutrition.Property(n => n.Fat).HasColumnName("Nutrition_Fat").HasPrecision(8, 2);
            // nutrition.Property(n => n.Fiber).HasColumnName("Nutrition_Fiber").HasPrecision(8, 2);
            // nutrition.Property(n => n.Sodium).HasColumnName("Nutrition_Sodium").HasPrecision(8, 2);
        });
        builder.Navigation(r => r.Nutrition).IsRequired();

        // D2: FK Category — RESTRICT, không bao giờ thật sự kích hoạt vì Category chỉ soft delete.
       builder.HasOne(r => r.Category)
       .WithMany(c => c.Recipes)
       .HasForeignKey(r => r.CategoryId)
       .OnDelete(DeleteBehavior.Restrict); // hoặc DeleteBehavior.Cascade tùy thiết kế
        

        // Author không bao giờ bị hard delete (D11 — chỉ deactivate qua IsActive).
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // D1: Recipe chỉ soft delete — FK CASCADE dưới đây là ràng buộc schema theo Chương 7,
        // trong thực tế không bao giờ kích hoạt vì không có hard delete.
        builder.HasMany(r => r.Steps)
            .WithOne()
            .HasForeignKey(s => s.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Ingredients)
            .WithOne()
            .HasForeignKey(i => i.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Images)
            .WithOne()
            .HasForeignKey(i => i.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
