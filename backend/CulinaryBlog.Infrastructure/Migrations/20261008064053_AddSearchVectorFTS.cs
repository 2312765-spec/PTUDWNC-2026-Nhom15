using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchVectorFTS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_AspNetUsers_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RecipeImages_RecipeId_IsPrimary",
                table: "RecipeImages");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Nutrition_Calories",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Nutrition_Carbohydrates",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Nutrition_Fat",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Nutrition_Fiber",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Nutrition_Protein",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Nutrition_Sodium",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "OrderIndex",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "MediumUrl",
                table: "RecipeImages");

            migrationBuilder.DropColumn(
                name: "ThumbnailUrl",
                table: "RecipeImages");

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "RefreshTokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "ReplacedByTokenHash",
                table: "RefreshTokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByIp",
                table: "RefreshTokens",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(45)",
                oldMaxLength: 45,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecipeId1",
                table: "RecipeSteps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Recipes",
                type: "tsvector",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Amount",
                table: "RecipeIngredients",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Preparation",
                table: "RecipeIngredients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecipeId1",
                table: "RecipeIngredients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OriginalUrl",
                table: "RecipeImages",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<int>(
                name: "OrderIndex",
                table: "RecipeImages",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<bool>(
                name: "IsPrimary",
                table: "RecipeImages",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "AltText",
                table: "RecipeImages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OrderIndex",
                table: "Categories",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "Categories",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSteps_RecipeId1",
                table: "RecipeSteps",
                column: "RecipeId1");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_SearchVector",
                table: "Recipes",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_RecipeId1",
                table: "RecipeIngredients",
                column: "RecipeId1");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredients_Recipes_RecipeId1",
                table: "RecipeIngredients",
                column: "RecipeId1",
                principalTable: "Recipes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeSteps_Recipes_RecipeId1",
                table: "RecipeSteps",
                column: "RecipeId1",
                principalTable: "Recipes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredients_Recipes_RecipeId1",
                table: "RecipeIngredients");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeSteps_Recipes_RecipeId1",
                table: "RecipeSteps");

            migrationBuilder.DropIndex(
                name: "IX_RecipeSteps_RecipeId1",
                table: "RecipeSteps");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_SearchVector",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_RecipeIngredients_RecipeId1",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "RecipeId1",
                table: "RecipeSteps");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "Preparation",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "RecipeId1",
                table: "RecipeIngredients");

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "RefreshTokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "ReplacedByTokenHash",
                table: "RefreshTokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByIp",
                table: "RefreshTokens",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "Recipes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Nutrition_Calories",
                table: "Recipes",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Nutrition_Carbohydrates",
                table: "Recipes",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Nutrition_Fat",
                table: "Recipes",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Nutrition_Fiber",
                table: "Recipes",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Nutrition_Protein",
                table: "Recipes",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Nutrition_Sodium",
                table: "Recipes",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "RecipeIngredients",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderIndex",
                table: "RecipeIngredients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "RecipeIngredients",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OriginalUrl",
                table: "RecipeImages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048);

            migrationBuilder.AlterColumn<int>(
                name: "OrderIndex",
                table: "RecipeImages",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<bool>(
                name: "IsPrimary",
                table: "RecipeImages",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "AltText",
                table: "RecipeImages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediumUrl",
                table: "RecipeImages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailUrl",
                table: "RecipeImages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OrderIndex",
                table: "Categories",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "Categories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeImages_RecipeId_IsPrimary",
                table: "RecipeImages",
                column: "RecipeId",
                unique: true,
                filter: "\"IsPrimary\" = true");

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_AspNetUsers_UserId",
                table: "RefreshTokens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
