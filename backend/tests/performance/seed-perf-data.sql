-- =========================================================================================
-- DỮ LIỆU GIẢ CHO ĐO HIỆU NĂNG (NFR-PERF-001..004)
-- CHỈ CHẠY TRÊN DB DEV/PERF — KHÔNG BAO GIỜ CHẠY TRÊN PRODUCTION.
--
-- Nhân 50 công thức của DbSeeder thành 10.000 bản (≈ 80% Published, CreatedAt rải trong 1 năm),
-- mỗi bản 1 ảnh. Với 50 dòng PostgreSQL luôn chọn Seq Scan nên EXPLAIN không có ý nghĩa.
-- SearchVector do trigger trg_recipes_search_vector tự tính.
-- Chạy lại được: xóa dữ liệu perf cũ (slug chứa '-perf-') trước khi sinh.
--
--   docker exec -i cb-postgres psql -U postgres -d culinaryblog < seed-perf-data.sql
-- =========================================================================================

BEGIN;

DELETE FROM "RecipeImages" WHERE "RecipeId" IN (SELECT "Id" FROM "Recipes" WHERE "Slug" LIKE '%-perf-%');
DELETE FROM "Recipes" WHERE "Slug" LIKE '%-perf-%';

WITH base AS (
    SELECT r.*, row_number() OVER (ORDER BY r."Id") - 1 AS rn, count(*) OVER () AS n
    FROM "Recipes" AS r
    WHERE r."Slug" NOT LIKE '%-perf-%'
),
gen AS (
    SELECT g, b.*,
           now() - (random() * interval '365 days') AS created_at,
           CASE WHEN g % 5 = 0 THEN 0 ELSE 1 END AS new_status   -- 1/5 Draft, 4/5 Published
    FROM generate_series(1, 10000) AS g
    JOIN base AS b ON b.rn = g % b.n
)
INSERT INTO "Recipes" (
    "Id", "Title", "Slug", "Description", "Instructions", "PrepTime", "CookTime", "Servings",
    "Difficulty", "Status", "CategoryId", "AuthorId", "PublishedAt",
    "Nutrition_Calories", "Nutrition_Protein", "Nutrition_Carbohydrates", "Nutrition_Fat",
    "Nutrition_Fiber", "Nutrition_Sodium", "CreatedAt", "UpdatedAt", "IsDeleted", "RowVersion")
SELECT gen_random_uuid(), "Title" || ' ' || g, "Slug" || '-perf-' || g, "Description", "Instructions",
       "PrepTime", "CookTime", "Servings", "Difficulty", new_status, "CategoryId", "AuthorId",
       CASE WHEN new_status = 1 THEN created_at END,
       "Nutrition_Calories", "Nutrition_Protein", "Nutrition_Carbohydrates", "Nutrition_Fat",
       "Nutrition_Fiber", "Nutrition_Sodium", created_at, NULL, FALSE,
       decode(md5(random()::text), 'hex')
FROM gen;

INSERT INTO "RecipeImages" (
    "Id", "RecipeId", "OriginalUrl", "MediumUrl", "ThumbnailUrl", "AltText",
    "IsPrimary", "OrderIndex", "CreatedAt", "UpdatedAt", "IsDeleted", "RowVersion")
SELECT gen_random_uuid(), r."Id",
       'perf/' || r."Id" || '/original.webp', 'perf/' || r."Id" || '/medium.webp',
       'perf/' || r."Id" || '/thumb.webp', r."Title",
       TRUE, 0, r."CreatedAt", NULL, FALSE, decode(md5(random()::text), 'hex')
FROM "Recipes" AS r
WHERE r."Slug" LIKE '%-perf-%';

COMMIT;

ANALYZE "Recipes";
ANALYZE "RecipeImages";

SELECT "Status", count(*) FROM "Recipes" WHERE NOT "IsDeleted" GROUP BY "Status" ORDER BY "Status";
