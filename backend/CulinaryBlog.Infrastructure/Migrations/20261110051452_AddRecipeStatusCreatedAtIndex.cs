using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeStatusCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Recipes_Status_CreatedAt",
                table: "Recipes",
                columns: new[] { "Status", "CreatedAt", "Id" },
                descending: new[] { false, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Recipes_Status_CreatedAt",
                table: "Recipes");
        }
    }
}
