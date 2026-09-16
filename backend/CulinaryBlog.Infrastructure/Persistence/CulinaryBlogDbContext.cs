using System.Linq.Expressions;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence;

/// <summary>
/// DbContext chính — CONS-006: PostgreSQL duy nhất, EF Core Code-First.
///
/// TODO(Sprint 0 — B): khi tạo ApplicationUser, đổi lớp cha thành
///   IdentityDbContext&lt;ApplicationUser, IdentityRole, string&gt;
/// và nhớ gọi base.OnModelCreating(modelBuilder) TRƯỚC phần cấu hình bên dưới.
/// </summary>
public class CulinaryBlogDbContext(DbContextOptions<CulinaryBlogDbContext> options)
    : DbContext(options), IUnitOfWork
{
    // TODO(Sprint 0 — B): khai báo DbSet cho Recipe, Category, RecipeStep,
    // RecipeIngredient, RecipeImage, RefreshToken theo SRS Chương 7.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CulinaryBlogDbContext).Assembly);

        ApplySoftDeleteQueryFilter(modelBuilder);
        ApplyRowVersionConcurrencyToken(modelBuilder);
    }

    /// <summary>
    /// D1/D2 — Global Query Filter cho soft delete.
    /// Áp tự động cho MỌI entity kế thừa BaseEntity, kể cả entity thêm sau này.
    /// Muốn đọc cả bản ghi đã xóa: dùng .IgnoreQueryFilters().
    /// </summary>
    private static void ApplySoftDeleteQueryFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.PropertyOrField(parameter, nameof(BaseEntity.IsDeleted));
            var filter = Expression.Lambda(Expression.Not(property), parameter);

            entityType.SetQueryFilter(filter);
        }
    }

    /// <summary>
    /// SRS 7.1 — RowVersion là concurrency token. Mismatch =&gt; 409 (D4).
    ///
    /// LƯU Ý Npgsql: KHÔNG dùng IsRowVersion() ở đây. IsRowVersion() = IsConcurrencyToken()
    /// + ValueGeneratedOnAddOrUpdate(), mà PostgreSQL không tự sinh giá trị cho cột bytea
    /// → migration/insert sẽ lỗi. Dùng IsConcurrencyToken() và để Application gán giá trị mới
    /// mỗi lần update.
    ///
    /// Phương án thay thế (nếu muốn PostgreSQL tự lo): bỏ cột RowVersion và dùng
    /// UseXminAsConcurrencyToken() — nhưng khác với SRS 7.1. Nếu đổi, ghi thành D23.
    /// </summary>
    private static void ApplyRowVersionConcurrencyToken(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(BaseEntity.RowVersion))
                .IsConcurrencyToken();
        }
    }
}
