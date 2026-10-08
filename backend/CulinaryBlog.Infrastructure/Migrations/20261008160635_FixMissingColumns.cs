using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixMissingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
    ALTER TABLE ""RecipeIngredients"" ADD COLUMN IF NOT EXISTS ""OrderIndex"" integer DEFAULT 0;
    ALTER TABLE ""RecipeImages"" ALTER COLUMN ""OrderIndex"" DROP NOT NULL;
    ALTER TABLE ""RecipeImages"" ALTER COLUMN ""OrderIndex"" SET DEFAULT 0;
    ALTER TABLE ""RecipeImages"" ALTER COLUMN ""IsPrimary"" DROP NOT NULL;
    ALTER TABLE ""RecipeImages"" ALTER COLUMN ""IsPrimary"" SET DEFAULT false;

    -- Đổi kiểu Calories và các chỉ số dinh dưỡng sang numeric để lưu được số thập phân 450.5
    DO $$ 
    BEGIN 
        -- Nếu cột Nutrition nằm trong bảng Recipes (kiểu JSON hoặc cột riêng) hoặc bảng RecipeNutritions:
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Recipes' AND column_name = 'Nutrition_Calories') THEN
            ALTER TABLE ""Recipes"" ALTER COLUMN ""Nutrition_Calories"" TYPE numeric USING ""Nutrition_Calories""::numeric;
        END IF;

        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Recipes' AND column_name = 'Calories') THEN
            ALTER TABLE ""Recipes"" ALTER COLUMN ""Calories"" TYPE numeric USING ""Calories""::numeric;
        END IF;

        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'RecipeNutritions' AND column_name = 'Calories') THEN
            ALTER TABLE ""RecipeNutritions"" ALTER COLUMN ""Calories"" TYPE numeric USING ""Calories""::numeric;
        END IF;

        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Nutritions' AND column_name = 'Calories') THEN
            ALTER TABLE ""Nutritions"" ALTER COLUMN ""Calories"" TYPE numeric USING ""Calories""::numeric;
        END IF;
    END $$;
        ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
