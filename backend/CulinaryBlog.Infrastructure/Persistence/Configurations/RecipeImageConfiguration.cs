using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public class RecipeImageConfiguration : IEntityTypeConfiguration<RecipeImage>
{
     public void Configure(EntityTypeBuilder<RecipeImage> builder)
    {
        builder.ToTable("RecipeImages");

        builder.HasKey(x => x.Id);

        // ĐẶC BIỆT QUAN TRỌNG: Cấu hình rõ ràng quan hệ với Recipe để triệt tiêu cột "RecipeId1"
        builder.HasOne(x => x.Recipe)
               .WithMany(r => r.Images)
               .HasForeignKey(x => x.RecipeId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Cascade);

        // Bỏ qua DisplayOrder vì nó chỉ là alias trong C#
        builder.Ignore(x => x.DisplayOrder);

        builder.Property(x => x.OrderIndex)
               .IsRequired();

        builder.Property(x => x.OriginalUrl)
               .IsRequired()
               .HasMaxLength(2048);

        builder.Property(x => x.AltText)
               .HasMaxLength(500);

        builder.Property(x => x.IsPrimary)
               .IsRequired();
    }
}