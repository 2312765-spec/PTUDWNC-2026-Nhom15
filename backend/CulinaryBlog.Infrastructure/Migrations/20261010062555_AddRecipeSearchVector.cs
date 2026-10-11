using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations
{
    /// <summary>
    /// FR-SRCH-001/D49 — cột SearchVector (tsvector) do trigger cập nhật khi Title/Description đổi,
    /// GIN index, điền sẵn cho dữ liệu cũ. Cấu hình 'simple' + unaccent (D49: PostgreSQL không có
    /// cấu hình 'vietnamese'). Title trọng số A, Description trọng số B → ts_rank ưu tiên khớp tiêu đề.
    /// Phần cột + index do EF sinh; hàm, trigger và backfill viết tay (EF không mô hình hóa trigger).
    /// </summary>
    public partial class AddRecipeSearchVector : Migration
    {
        private const string BuildVectorFunction = """
            CREATE OR REPLACE FUNCTION recipe_search_vector(title text, description text)
            RETURNS tsvector
            LANGUAGE sql STABLE AS $$
                SELECT setweight(to_tsvector('simple', unaccent(coalesce(title, ''))), 'A')
                    || setweight(to_tsvector('simple', unaccent(coalesce(description, ''))), 'B')
            $$;
            """;

        private const string TriggerFunction = """
            CREATE OR REPLACE FUNCTION recipes_search_vector_trigger()
            RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                NEW."SearchVector" := recipe_search_vector(NEW."Title", NEW."Description");
                RETURN NEW;
            END
            $$;
            """;

        private const string Trigger = """
            CREATE TRIGGER trg_recipes_search_vector
            BEFORE INSERT OR UPDATE OF "Title", "Description" ON "Recipes"
            FOR EACH ROW EXECUTE FUNCTION recipes_search_vector_trigger();
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Recipes",
                type: "tsvector",
                nullable: true);

            migrationBuilder.Sql(BuildVectorFunction);
            migrationBuilder.Sql(TriggerFunction);
            migrationBuilder.Sql(Trigger);
            migrationBuilder.Sql("""UPDATE "Recipes" SET "SearchVector" = recipe_search_vector("Title", "Description");""");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_SearchVector",
                table: "Recipes",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS trg_recipes_search_vector ON "Recipes";""");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS recipes_search_vector_trigger();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS recipe_search_vector(text, text);");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_SearchVector",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Recipes");
        }
    }
}
