-- =========================================================================================
-- EXPLAIN ANALYZE — NFR-PERF-004 (PostgreSQL 16)
--
-- Các câu SQL dưới đây là SQL THẬT do EF Core sinh ra (lấy từ log
-- Microsoft.EntityFrameworkCore.Database.Command của API ở môi trường Development),
-- chỉ thay tham số @p bằng giá trị cụ thể. Đổi LINQ trong repository thì phải lấy lại SQL.
--
-- Chạy trên dữ liệu của seed-perf-data.sql (≈ 10.000 recipes) — với 50 dòng của DbSeeder
-- PostgreSQL luôn chọn Seq Scan nên kết quả không có ý nghĩa.
--
--   docker exec -i cb-postgres psql -U postgres -d culinaryblog < explain-analyze.sql
-- =========================================================================================

-- -----------------------------------------------------------------------------------------
-- 1. FR-CAT-001 — GET /api/v1/categories. NFR-PERF-004 (2026-11-10): projection, recipeCount đếm
-- bằng subquery COUNT trong PostgreSQL — không nạp recipe/ảnh. (Trước đó: Include Recipes + Images,
-- nạp 8.017 dòng, sort tràn đĩa — xem perf-summary.md.)
-- -----------------------------------------------------------------------------------------
EXPLAIN (ANALYZE, BUFFERS)
SELECT c."Id", c."Name", c."Slug", c."Description", (
    SELECT count(*)::int
    FROM "Recipes" AS r
    WHERE NOT (r."IsDeleted") AND c."Id" = r."CategoryId" AND NOT (r."IsDeleted") AND r."Status" = 1)
FROM "Categories" AS c
WHERE NOT (c."IsDeleted")
ORDER BY c."Name";

-- -----------------------------------------------------------------------------------------
-- 2. FR-RCP-001 / FR-SRCH-004 — GET /api/v1/recipes?page=1&pageSize=12 (sort mặc định -createdAt)
-- 2a. COUNT trước SKIP/TAKE
-- -----------------------------------------------------------------------------------------
EXPLAIN (ANALYZE, BUFFERS)
SELECT count(*)::int
FROM "Recipes" AS r
WHERE NOT (r."IsDeleted") AND r."Status" = 1;

-- 2b. Trang dữ liệu — dùng IX_Recipes_Status_CreatedAt ("Status", "CreatedAt" DESC, "Id"), NFR-PERF-004
EXPLAIN (ANALYZE, BUFFERS)
SELECT r1."Id", r1."AuthorId", r1."CategoryId", r1."CookTime", r1."CreatedAt", r1."Description", r1."Difficulty", r1."Instructions", r1."IsDeleted", r1."PrepTime", r1."PublishedAt", r1."RowVersion", r1."SearchVector", r1."Servings", r1."Slug", r1."Status", r1."Title", r1."UpdatedAt", r1."Nutrition_Calories", r1."Nutrition_Carbohydrates", r1."Nutrition_Fat", r1."Nutrition_Fiber", r1."Nutrition_Protein", r1."Nutrition_Sodium", r2."Id", r2."AltText", r2."CardUrl", r2."CreatedAt", r2."IsDeleted", r2."IsPrimary", r2."MediumUrl", r2."OrderIndex", r2."OriginalUrl", r2."RecipeId", r2."RowVersion", r2."ThumbnailUrl", r2."UpdatedAt"
FROM (
    SELECT r."Id", r."AuthorId", r."CategoryId", r."CookTime", r."CreatedAt", r."Description", r."Difficulty", r."Instructions", r."IsDeleted", r."PrepTime", r."PublishedAt", r."RowVersion", r."SearchVector", r."Servings", r."Slug", r."Status", r."Title", r."UpdatedAt", r."Nutrition_Calories", r."Nutrition_Carbohydrates", r."Nutrition_Fat", r."Nutrition_Fiber", r."Nutrition_Protein", r."Nutrition_Sodium"
    FROM "Recipes" AS r
    WHERE NOT (r."IsDeleted") AND r."Status" = 1
    ORDER BY r."CreatedAt" DESC, r."Id"
    LIMIT 12 OFFSET 0
) AS r1
LEFT JOIN (
    SELECT r0."Id", r0."AltText", r0."CardUrl", r0."CreatedAt", r0."IsDeleted", r0."IsPrimary", r0."MediumUrl", r0."OrderIndex", r0."OriginalUrl", r0."RecipeId", r0."RowVersion", r0."ThumbnailUrl", r0."UpdatedAt"
    FROM "RecipeImages" AS r0
    WHERE NOT (r0."IsDeleted")
) AS r2 ON r1."Id" = r2."RecipeId"
ORDER BY r1."CreatedAt" DESC, r1."Id";

-- -----------------------------------------------------------------------------------------
-- 3. FR-RCP-002 — GET /api/v1/recipes/{slug}. AsSplitQuery: 1 câu chính + 3 câu con
-- (Steps/Ingredients/Images) — số câu CỐ ĐỊNH, không phải N+1. Đo câu chính.
-- -----------------------------------------------------------------------------------------
EXPLAIN (ANALYZE, BUFFERS)
SELECT r."Id", r."AuthorId", r."CategoryId", r."CookTime", r."CreatedAt", r."Description", r."Difficulty", r."Instructions", r."IsDeleted", r."PrepTime", r."PublishedAt", r."RowVersion", r."SearchVector", r."Servings", r."Slug", r."Status", r."Title", r."UpdatedAt", r."Nutrition_Calories", r."Nutrition_Carbohydrates", r."Nutrition_Fat", r."Nutrition_Fiber", r."Nutrition_Protein", r."Nutrition_Sodium", c0."Id", c0."CreatedAt", c0."Description", c0."ImageUrl", c0."IsDeleted", c0."Name", c0."OrderIndex", c0."RowVersion", c0."Slug", c0."UpdatedAt"
FROM "Recipes" AS r
INNER JOIN (
    SELECT c."Id", c."CreatedAt", c."Description", c."ImageUrl", c."IsDeleted", c."Name", c."OrderIndex", c."RowVersion", c."Slug", c."UpdatedAt"
    FROM "Categories" AS c
    WHERE NOT (c."IsDeleted")
) AS c0 ON r."CategoryId" = c0."Id"
WHERE NOT (r."IsDeleted") AND r."Slug" = 'mi-quang-ga' AND NOT (r."IsDeleted")
ORDER BY r."Id", c0."Id"
LIMIT 1;

-- -----------------------------------------------------------------------------------------
-- 4. FR-SRCH-001 / D49 — GET /api/v1/recipes/search?q=pho
-- RecipeRepository.SearchPublishedRecipesAsync: to_tsquery('simple', unaccent('pho:*')).
-- 4a. COUNT
-- -----------------------------------------------------------------------------------------
EXPLAIN (ANALYZE, BUFFERS)
SELECT count(*)::int
FROM "Recipes" AS r
WHERE NOT (r."IsDeleted") AND NOT (r."IsDeleted") AND r."Status" = 1 AND r."SearchVector" @@ to_tsquery('simple', unaccent('pho:*'));

-- 4b. Xếp hạng ts_rank + phân trang
EXPLAIN (ANALYZE, BUFFERS)
SELECT r."Id", ts_rank(r."SearchVector", to_tsquery('simple', unaccent('pho:*'))) AS "Rank", r."CreatedAt"
FROM "Recipes" AS r
WHERE NOT (r."IsDeleted") AND NOT (r."IsDeleted") AND r."Status" = 1 AND r."SearchVector" @@ to_tsquery('simple', unaccent('pho:*'))
ORDER BY ts_rank(r."SearchVector", to_tsquery('simple', unaccent('pho:*'))) DESC, r."CreatedAt" DESC, r."Id"
LIMIT 10 OFFSET 0;

-- -----------------------------------------------------------------------------------------
-- 5. FR-CAT-002 — GET /api/v1/categories/mon-chay?page=2&pageSize=12 (Guest). NFR-PERF-004
-- (2026-11-10): COUNT + LIMIT/OFFSET trong PostgreSQL. (Trước đó: Include toàn bộ recipe + ảnh
-- của danh mục rồi lọc/phân trang trong bộ nhớ.) @categoryId lấy bằng \gset.
-- -----------------------------------------------------------------------------------------
SELECT "Id" AS category_id FROM "Categories" WHERE "Slug" = 'mon-chay' \gset

-- 5a. COUNT
EXPLAIN (ANALYZE, BUFFERS)
SELECT count(*)::int
FROM "Recipes" AS r
WHERE NOT (r."IsDeleted") AND r."CategoryId" = :'category_id' AND NOT (r."IsDeleted") AND r."Status" = 1;

-- 5b. Trang 2
EXPLAIN (ANALYZE, BUFFERS)
SELECT r1."Id", r1."AuthorId", r1."CategoryId", r1."CookTime", r1."CreatedAt", r1."Description", r1."Difficulty", r1."Instructions", r1."IsDeleted", r1."PrepTime", r1."PublishedAt", r1."RowVersion", r1."SearchVector", r1."Servings", r1."Slug", r1."Status", r1."Title", r1."UpdatedAt", r1."Nutrition_Calories", r1."Nutrition_Carbohydrates", r1."Nutrition_Fat", r1."Nutrition_Fiber", r1."Nutrition_Protein", r1."Nutrition_Sodium", r2."Id", r2."AltText", r2."CardUrl", r2."CreatedAt", r2."IsDeleted", r2."IsPrimary", r2."MediumUrl", r2."OrderIndex", r2."OriginalUrl", r2."RecipeId", r2."RowVersion", r2."ThumbnailUrl", r2."UpdatedAt"
FROM (
    SELECT r."Id", r."AuthorId", r."CategoryId", r."CookTime", r."CreatedAt", r."Description", r."Difficulty", r."Instructions", r."IsDeleted", r."PrepTime", r."PublishedAt", r."RowVersion", r."SearchVector", r."Servings", r."Slug", r."Status", r."Title", r."UpdatedAt", r."Nutrition_Calories", r."Nutrition_Carbohydrates", r."Nutrition_Fat", r."Nutrition_Fiber", r."Nutrition_Protein", r."Nutrition_Sodium"
    FROM "Recipes" AS r
    WHERE NOT (r."IsDeleted") AND r."CategoryId" = :'category_id' AND NOT (r."IsDeleted") AND r."Status" = 1
    ORDER BY r."CreatedAt" DESC, r."Id"
    LIMIT 12 OFFSET 12
) AS r1
LEFT JOIN (
    SELECT r0."Id", r0."AltText", r0."CardUrl", r0."CreatedAt", r0."IsDeleted", r0."IsPrimary", r0."MediumUrl", r0."OrderIndex", r0."OriginalUrl", r0."RecipeId", r0."RowVersion", r0."ThumbnailUrl", r0."UpdatedAt"
    FROM "RecipeImages" AS r0
    WHERE NOT (r0."IsDeleted")
) AS r2 ON r1."Id" = r2."RecipeId"
ORDER BY r1."CreatedAt" DESC, r1."Id";
