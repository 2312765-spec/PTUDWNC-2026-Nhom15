using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations;

/// <summary>
/// FR-SRCH-001: bật extension unaccent cho SearchPublishedRecipesAsync.
/// scripts/init-db.sql chỉ chạy khi volume docker rỗng — Testcontainers/prod không có nó.
///
/// Viết tay, KHÔNG dùng "dotnet ef migrations add": snapshot hiện đang lệch model
/// (Nutrition_*, Instructions, RecipeIngredients.Quantity…) nên migration tự sinh
/// sẽ DROP hàng loạt cột. Không đụng tới snapshot.
/// </summary>
[DbContext(typeof(CulinaryBlogDbContext))]
[Migration("20260930063825_AddUnaccentExtension")]
public partial class AddUnaccentExtension : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP EXTENSION IF EXISTS unaccent;");
    }
}
