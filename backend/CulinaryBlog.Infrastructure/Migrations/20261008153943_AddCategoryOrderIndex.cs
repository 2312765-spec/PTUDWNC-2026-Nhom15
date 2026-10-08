using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryOrderIndex : Migration
    {
        /// <inheritdoc />
       protected override void Up(MigrationBuilder migrationBuilder)
        {
                migrationBuilder.AddColumn<string>(
            name: "MediumUrl",
            table: "RecipeImages",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Notes",
            table: "RecipeIngredients",
            type: "text",
            nullable: true);
            migrationBuilder.AddColumn<string>(
            name: "ThumbnailUrl",
            table: "RecipeImages",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "Quantity",
            table: "RecipeIngredients",
            type: "numeric",
            nullable: true);
        }

    protected override void Down(MigrationBuilder migrationBuilder)
        {
                    migrationBuilder.DropColumn(
            name: "MediumUrl",
            table: "RecipeImages");

        migrationBuilder.DropColumn(
            name: "Notes",
            table: "RecipeIngredients");
        }
    }
}
