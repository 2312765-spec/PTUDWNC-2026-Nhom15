using System.Linq.Expressions;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using CulinaryBlog.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence;

/// <summary>
/// DbContext chính — CONS-006: PostgreSQL duy nhất, EF Core Code-First.
/// D23/ADR-0003: kế thừa IdentityDbContext để có AspNetUsers/AspNetRoles… (ApplicationUser
/// ở Infrastructure, không phải Domain — xem ADR-0003).
/// </summary>
public class CulinaryBlogDbContext(DbContextOptions<CulinaryBlogDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options), IUnitOfWork
{
    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        await action();
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        var result = await action();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        var result = await action(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeImage> RecipeImages => Set<RecipeImage>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Recipe>(entity =>
{
         // Bỏ qua Nutrition để EF Core không tìm kiếm các cột Nutrition_Carbs, Nutrition_Calories... trong database
         entity.Ignore(r => r.Nutrition);
        });
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CulinaryBlogDbContext).Assembly);
        
        // Đảm bảo quan hệ 1-N giữa Recipe và RecipeImage sử dụng đúng cột
        modelBuilder.Entity<RecipeImage>(entity =>
        {
            entity.ToTable("RecipeImages");
            entity.HasKey(e => e.Id);

            entity.HasOne(d => d.Recipe)
                .WithMany(p => p.Images)
                .HasForeignKey(d => d.RecipeId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            entity.Ignore(e => e.DisplayOrder);
        });

        // THÊM ĐOẠN NÀY VÀO NGAY ĐÂY:
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.Token);
        });

        ApplySoftDeleteQueryFilter(modelBuilder);
    }

    /// <inheritdoc />
    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default)
    {
        // EnableRetryOnFailure đang bật (DependencyInjection.cs) nên transaction thủ công BẮT BUỘC
        // đi qua execution strategy. Retry = chạy lại cả khối: tracker còn giữ trạng thái của lần
        // đã rollback nên phải Clear để operation nạp lại entity từ DB.
        var strategy = Database.CreateExecutionStrategy();
        var attempt = 0;

        return strategy.ExecuteAsync(async () =>
        {
            if (attempt++ > 0)
            {
                ChangeTracker.Clear();
            }

            await using var transaction = await Database.BeginTransactionAsync(ct);
            await operation(ct);
            await transaction.CommitAsync(ct);
        });
    }

    /// <summary>
    /// D1/D2 — Global Query Filter cho soft delete.
    /// Áp tự động cho MỌI entity kế thừa BaseEntity (trừ Owned Entities), kể cả entity thêm sau này.
    /// Muốn đọc cả bản ghi đã xóa: dùng .IgnoreQueryFilters().
    /// </summary>
    private static void ApplySoftDeleteQueryFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // BỎ QUA Owned Entity Types (như RecipeNutrition) vì EF Core không cho áp Query Filter lên Owned Types
            if (entityType.IsOwned())
            {
                continue;
            }

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
    /// SRS 7.1 — RowVersion là concurrency token. Mismatch => 409 (D4).
    /// </summary>
    private static void ApplyRowVersionConcurrencyToken(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // BỎ QUA Owned Entity Types để tránh lỗi re-configuring thành non-owned entity
            if (entityType.IsOwned())
            {
                continue;
            }

            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property<byte[]>("RowVersion")
                    .IsConcurrencyToken(); // Dùng IsConcurrencyToken thay vì IsRowVersion cho tương thích PostgreSQL
            }
        }
    }
}