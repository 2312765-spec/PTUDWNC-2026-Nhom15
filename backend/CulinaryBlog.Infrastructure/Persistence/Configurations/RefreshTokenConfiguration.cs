using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

/// <summary>SRS 7.8 / D20 — schema chính xác của bảng RefreshTokens.</summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(rt => rt.Id);

        // Bỏ qua các thuộc tính không có trong bảng Database
        builder.Ignore(x => x.UpdatedAt);
        builder.Ignore(x => x.IsActive);
        builder.Ignore(x => x.IsExpired);
        builder.Ignore(x => x.IsRevoked);

        builder.Property(rt => rt.UserId)
            .IsRequired()
            .HasMaxLength(450);

        // D20 — SHA-256 dạng hex: đúng 64 ký tự (JwtService.HashToken).
        builder.Property(rt => rt.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(rt => rt.TokenHash)
            .IsUnique();

        builder.Property(rt => rt.ExpiresAt)
            .IsRequired();

        builder.Property(rt => rt.CreatedAt)
            .IsRequired();

        // 45 = độ dài tối đa của địa chỉ IPv6 dạng chuỗi.
        builder.Property(rt => rt.CreatedByIp)
            .HasMaxLength(45);

        builder.Property(rt => rt.ReplacedByTokenHash)
            .HasMaxLength(64);

        // Xóa user → xóa luôn refresh token của user đó, không để token mồ côi.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rt => rt.UserId);
    }
}
