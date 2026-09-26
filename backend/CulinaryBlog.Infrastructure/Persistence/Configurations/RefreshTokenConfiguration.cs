using CulinaryBlog.Domain.Entities;
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
        builder.Ignore(x => x.Token);
        builder.Ignore(x => x.IsActive);
        builder.Ignore(x => x.IsExpired);
        builder.Ignore(x => x.IsRevoked);

        builder.Property(rt => rt.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(rt => rt.TokenHash)
            .IsRequired()
            .HasMaxLength(256);

        // Ràng buộc Unique Index cho TokenHash
        builder.HasIndex(rt => rt.TokenHash)
            .IsUnique();

        builder.Property(rt => rt.ExpiresAt)
            .IsRequired();

        builder.Property(rt => rt.CreatedAt)
            .IsRequired();

        builder.Property(rt => rt.CreatedByIp)
            .HasMaxLength(50);

        builder.Property(rt => rt.ReplacedByTokenHash)
            .HasMaxLength(256);
    }
}