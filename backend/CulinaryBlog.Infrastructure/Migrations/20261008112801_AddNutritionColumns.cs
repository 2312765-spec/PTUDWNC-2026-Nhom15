using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations;

[DbContext(typeof(CulinaryBlogDbContext))]
[Migration("20261008112801_AddNutritionColumns")]
public partial class AddNutritionColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Nutrition_Calories",
            table: "Recipes",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "Nutrition_Fat",
            table: "Recipes",
            type: "numeric",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "Nutrition_Protein",
            table: "Recipes",
            type: "numeric",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "Nutrition_Carbohydrates",
            table: "Recipes",
            type: "numeric",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "Nutrition_Fiber",
            table: "Recipes",
            type: "numeric",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "Nutrition_Sugar",
            table: "Recipes",
            type: "numeric",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "Nutrition_Sodium",
            table: "Recipes",
            type: "numeric",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Nutrition_Calories", table: "Recipes");
        migrationBuilder.DropColumn(name: "Nutrition_Fat", table: "Recipes");
        migrationBuilder.DropColumn(name: "Nutrition_Protein", table: "Recipes");
        migrationBuilder.DropColumn(name: "Nutrition_Carbohydrates", table: "Recipes");
        migrationBuilder.DropColumn(name: "Nutrition_Fiber", table: "Recipes");
        migrationBuilder.DropColumn(name: "Nutrition_Sugar", table: "Recipes");
        migrationBuilder.DropColumn(name: "Nutrition_Sodium", table: "Recipes");
    }
}