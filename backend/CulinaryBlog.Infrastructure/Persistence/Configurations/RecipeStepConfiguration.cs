using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

/// <summary>SRS 7.3 + D6.</summary>
public sealed class RecipeStepConfiguration : IEntityTypeConfiguration<RecipeStep>
{
    public void Configure(EntityTypeBuilder<RecipeStep> builder)
    {
        builder.ToTable("RecipeSteps", t =>
            t.HasCheckConstraint("CK_RecipeSteps_StepNumber", "\"StepNumber\" > 0"));

        builder.Property(s => s.Title).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Description).IsRequired();
        builder.Property(s => s.ImageUrl).HasMaxLength(500);

        builder.HasIndex(s => new { s.RecipeId, s.StepNumber }).IsUnique();
    }
}
