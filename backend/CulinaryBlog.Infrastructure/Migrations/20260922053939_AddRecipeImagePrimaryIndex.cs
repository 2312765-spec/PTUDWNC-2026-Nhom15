using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeImagePrimaryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RecipeImages_RecipeId_IsPrimary",
                table: "RecipeImages",
                column: "RecipeId",
                unique: true,
                filter: "\"IsPrimary\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecipeImages_RecipeId_IsPrimary",
                table: "RecipeImages");
        }
    }
}
