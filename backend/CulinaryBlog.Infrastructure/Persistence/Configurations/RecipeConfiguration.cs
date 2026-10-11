using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

/// <summary>
/// SRS 7.2 + 7.2.1. D18 (Instructions nullable) · D19 (CookTime >= 0, PrepTime/Servings > 0).
/// </summary>
public sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    /// <summary>
    /// FR-SRCH-001/D49 — shadow property (Domain không biết NpgsqlTsVector, CONS-001). Trigger
    /// <c>trg_recipes_search_vector</c> ghi cột này; EF chỉ đọc khi truy vấn.
    /// </summary>
    public const string SearchVector = "SearchVector";

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

        // FR-SRCH-001/D49 — tsvector do trigger cập nhật, GIN index cho tìm kiếm toàn văn.
        var searchVector = builder.Property<NpgsqlTsVector>(SearchVector).HasColumnType("tsvector");
        searchVector.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        searchVector.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        builder.HasIndex(SearchVector).HasMethod("GIN").HasDatabaseName("IX_Recipes_SearchVector");

        // D18 — legacy, nullable.
        builder.Property(r => r.Instructions);

        // SRS 7.2.1 — owned, cột "Nutrition_*" nằm ngay trong bảng Recipes.
        builder.OwnsOne(r => r.Nutrition, n =>
        {
            n.Property(x => x.Calories).HasColumnName("Nutrition_Calories").HasPrecision(8, 2);
            n.Property(x => x.Protein).HasColumnName("Nutrition_Protein").HasPrecision(8, 2);
            n.Property(x => x.Carbohydrates).HasColumnName("Nutrition_Carbohydrates").HasPrecision(8, 2);
            n.Property(x => x.Fat).HasColumnName("Nutrition_Fat").HasPrecision(8, 2);
            n.Property(x => x.Fiber).HasColumnName("Nutrition_Fiber").HasPrecision(8, 2);
            n.Property(x => x.Sodium).HasColumnName("Nutrition_Sodium").HasPrecision(8, 2);
        });
        builder.Navigation(r => r.Nutrition).IsRequired();

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

        // FK Steps / Ingredients — PHẢI chỉ rõ navigation ngược (s.Recipe / i.Recipe). Để trống
        // .WithOne() thì EF coi navigation Recipe trên entity con là quan hệ THỨ HAI và tự sinh
        // cột shadow "RecipeId1".
        builder.HasMany(r => r.Steps)
            .WithOne(s => s.Recipe)
            .HasForeignKey(s => s.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Ingredients)
            .WithOne(i => i.Recipe)
            .HasForeignKey(i => i.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK Images
        builder.HasMany(r => r.Images)
            .WithOne(i => i.Recipe)
            .HasForeignKey(i => i.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}