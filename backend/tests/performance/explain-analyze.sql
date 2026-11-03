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
-- 1. FR-CAT-001 — GET /api/v1/categories (Include Recipes + Images).
-- recipeCount được đếm trong bộ nhớ sau khi nạp TOÀN BỘ recipe Published + ảnh.
-- -----------------------------------------------------------------------------------------
EXPLAIN (ANALYZE, BUFFERS)
SELECT c."Id", c."CreatedAt", c."Description", c."ImageUrl", c."IsDeleted", c."Name", c."OrderIndex", c."RowVersion", c."Slug", c."UpdatedAt", s."Id", s."AuthorId", s."CategoryId", s."CookTime", s."CreatedAt", s."Description", s."Difficulty", s."Instructions", s."IsDeleted", s."PrepTime", s."PublishedAt", s."RowVersion", s."SearchVector", s."Servings", s."Slug", s."Status", s."Title", s."UpdatedAt", s."Nutrition_Calories", s."Nutrition_Carbohydrates", s."Nutrition_Fat", s."Nutrition_Fiber", s."Nutrition_Protein", s."Nutrition_Sodium", s."Id0", s."AltText", s."CreatedAt0", s."IsDeleted0", s."IsPrimary", s."MediumUrl", s."OrderIndex", s."OriginalUrl", s."RecipeId", s."RowVersion0", s."ThumbnailUrl", s."UpdatedAt0"
FROM "Categories" AS c
LEFT JOIN (
    SELECT r."Id", r."AuthorId", r."CategoryId", r."CookTime", r."CreatedAt", r."Description", r."Difficulty", r."Instructions", r."IsDeleted", r."PrepTime", r."PublishedAt", r."RowVersion", r."SearchVector", r."Servings", r."Slug", r."Status", r."Title", r."UpdatedAt", r."Nutrition_Calories", r."Nutrition_Carbohydrates", r."Nutrition_Fat", r."Nutrition_Fiber", r."Nutrition_Protein", r."Nutrition_Sodium", r1."Id" AS "Id0", r1."AltText", r1."CreatedAt" AS "CreatedAt0", r1."IsDeleted" AS "IsDeleted0", r1."IsPrimary", r1."MediumUrl", r1."OrderIndex", r1."OriginalUrl", r1."RecipeId", r1."RowVersion" AS "RowVersion0", r1."ThumbnailUrl", r1."UpdatedAt" AS "UpdatedAt0"
    FROM "Recipes" AS r
    LEFT JOIN (
        SELECT r0."Id", r0."AltText", r0."CreatedAt", r0."IsDeleted", r0."IsPrimary", r0."MediumUrl", r0."OrderIndex", r0."OriginalUrl", r0."RecipeId", r0."RowVersion", r0."ThumbnailUrl", r0."UpdatedAt"
        FROM "RecipeImages" AS r0
        WHERE NOT (r0."IsDeleted")
    ) AS r1 ON r."Id" = r1."RecipeId"
    WHERE NOT (r."IsDeleted") AND NOT (r."IsDeleted") AND r."Status" = 1
) AS s ON c."Id" = s."CategoryId"
WHERE NOT (c."IsDeleted")
ORDER BY c."Name", c."Id", s."Id";

-- -----------------------------------------------------------------------------------------
-- 2. FR-RCP-001 / FR-SRCH-004 — GET /api/v1/recipes?page=1&pageSize=12 (sort mặc định -createdAt)
-- 2a. COUNT trước SKIP/TAKE
-- -----------------------------------------------------------------------------------------
EXPLAIN (ANALYZE, BUFFERS)
SELECT count(*)::int
FROM "Recipes" AS r
WHERE NOT (r."IsDeleted") AND r."Status" = 1;

-- 2b. Trang dữ liệu
EXPLAIN (ANALYZE, BUFFERS)
SELECT r1."Id", r1."AuthorId", r1."CategoryId", r1."CookTime", r1."CreatedAt", r1."Description", r1."Difficulty", r1."Instructions", r1."IsDeleted", r1."PrepTime", r1."PublishedAt", r1."RowVersion", r1."SearchVector", r1."Servings", r1."Slug", r1."Status", r1."Title", r1."UpdatedAt", r1."Nutrition_Calories", r1."Nutrition_Carbohydrates", r1."Nutrition_Fat", r1."Nutrition_Fiber", r1."Nutrition_Protein", r1."Nutrition_Sodium", r2."Id", r2."AltText", r2."CreatedAt", r2."IsDeleted", r2."IsPrimary", r2."MediumUrl", r2."OrderIndex", r2."OriginalUrl", r2."RecipeId", r2."RowVersion", r2."ThumbnailUrl", r2."UpdatedAt"
FROM (
    SELECT r."Id", r."AuthorId", r."CategoryId", r."CookTime", r."CreatedAt", r."Description", r."Difficulty", r."Instructions", r."IsDeleted", r."PrepTime", r."PublishedAt", r."RowVersion", r."SearchVector", r."Servings", r."Slug", r."Status", r."Title", r."UpdatedAt", r."Nutrition_Calories", r."Nutrition_Carbohydrates", r."Nutrition_Fat", r."Nutrition_Fiber", r."Nutrition_Protein", r."Nutrition_Sodium"
    FROM "Recipes" AS r
    WHERE NOT (r."IsDeleted") AND r."Status" = 1
    ORDER BY r."CreatedAt" DESC, r."Id"
    LIMIT 12 OFFSET 0
) AS r1
LEFT JOIN (
    SELECT r0."Id", r0."AltText", r0."CreatedAt", r0."IsDeleted", r0."IsPrimary", r0."MediumUrl", r0."OrderIndex", r0."OriginalUrl", r0."RecipeId", r0."RowVersion", r0."ThumbnailUrl", r0."UpdatedAt"
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
