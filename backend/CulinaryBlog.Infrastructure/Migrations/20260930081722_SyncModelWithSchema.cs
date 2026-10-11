using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations
{
    /// <summary>
    /// CỐ Ý RỖNG — không đổi schema. Code entity/config đã bị sửa lệch khỏi DB (Nutrition bị Ignore,
    /// RecipeIngredient đổi sang Amount/Preparation, mất MediumUrl/ThumbnailUrl, TokenHash 256…)
    /// trong khi DB vẫn đúng SRS. Đã đưa code về khớp DB; migration này chỉ cập nhật snapshot
    /// (thêm tên navigation Recipe/Category) để lần "migrations add" sau không sinh DROP COLUMN.
    /// </summary>
    public partial class SyncModelWithSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
