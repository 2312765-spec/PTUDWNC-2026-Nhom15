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

        builder.Property(rt => rt.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(rt => rt.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        // Không đặt HasDatabaseName riêng — khớp tên mặc định IX_RefreshTokens_TokenHash mà
        // migration InitialCreate (Sprint 0) đã tạo, tránh sinh migration đổi tên index vô ích.
        builder.HasIndex(rt => rt.TokenHash)
            .IsUnique();

        builder.HasIndex(rt => rt.UserId);

        builder.Property(rt => rt.ReplacedByTokenHash)
            .HasMaxLength(64);

        builder.Property(rt => rt.CreatedByIp)
            .HasMaxLength(45);

        // D20: IsRevoked/IsExpired/IsActive là computed property trong C#, không phải cột DB.
        builder.Ignore(rt => rt.IsRevoked);
        builder.Ignore(rt => rt.IsExpired);
        builder.Ignore(rt => rt.IsActive);

        // D23: Domain không có navigation property tới ApplicationUser — FK cấu hình một chiều ở đây.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
