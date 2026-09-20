using CulinaryBlog.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

/// <summary>SRS 7.8 + D20 — bảng "RefreshTokens" (PascalCase, không phải "refresh_tokens").</summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.UserId).HasMaxLength(450).IsRequired();
        builder.Property(rt => rt.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(rt => rt.ReplacedByTokenHash).HasMaxLength(64);
        builder.Property(rt => rt.CreatedByIp).HasMaxLength(45);

        builder.HasIndex(rt => rt.TokenHash).IsUnique();
        builder.HasIndex(rt => rt.UserId);

        // D20: IsRevoked/IsActive là property tính toán trong C#, không phải cột DB.
        builder.Ignore(rt => rt.IsRevoked);
        builder.Ignore(rt => rt.IsActive);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
