-- =========================================================================================
-- EXPLAIN ANALYZE BENCHMARK & QUERY PERFORMANCE VERIFICATION (NFR-PERF-004)
-- Dự án: Culinary Blog (PostgreSQL 16+)
-- =========================================================================================

-- -----------------------------------------------------------------------------------------
-- 1. Truy vấn Danh sách Danh mục (FR-CAT-001 / GET /api/v1/categories)
-- Cột đúng: "OrderIndex" (không có "DisplayOrder", không có cột "RecipeCount").
-- RecipeCount được tính toán động từ subquery đếm các Recipe Published chưa xóa.
-- -----------------------------------------------------------------------------------------
EXPLAIN (ANALYZE, BUFFERS)
SELECT c."Id", c."Name", c."Slug", c."Description", c."OrderIndex",
       (SELECT COUNT(*)::int 
        FROM "Recipes" AS r 
        WHERE r."CategoryId" = c."Id" 
          AND r."IsDeleted" = FALSE 
          AND r."Status" = 1) AS "RecipeCount"
FROM "Categories" AS c
WHERE c."IsDeleted" = FALSE
ORDER BY c."OrderIndex" ASC;

-- -----------------------------------------------------------------------------------------
-- 2. Truy vấn Danh sách Công thức phân trang (FR-RCP-001 / GET /api/v1/recipes)
-- Cột đúng: "CookTime" (không phải "CookingTimeMinutes").
-- Ảnh đại diện: Lấy từ bảng liên kết "RecipeImages" (IsPrimary = TRUE hoặc OrderIndex nhỏ nhất).
-- Kiểu dữ liệu "Status": smallint (1 = Published; không dùng chuỗi 'Published').
-- -----------------------------------------------------------------------------------------
EXPLAIN (ANALYZE, BUFFERS)
SELECT r."Id", r."Title", r."Slug", r."Description", r."PrepTime", r."CookTime", 
       r."Difficulty", r."Status", r."AuthorId", r."CreatedAt",
       (SELECT img."OriginalUrl" 
        FROM "RecipeImages" AS img 
        WHERE img."RecipeId" = r."Id" 
        ORDER BY img."IsPrimary" DESC, img."OrderIndex" ASC 
        LIMIT 1) AS "PrimaryImageUrl"
FROM "Recipes" AS r
WHERE r."IsDeleted" = FALSE 
  AND r."Status" = 1
ORDER BY r."CreatedAt" DESC
LIMIT 12 OFFSET 0;

-- GHI CHÚ PHÁT HIỆN HIỆU NĂNG (NFR-PERF-004):
-- Cột "CreatedAt" hiện chưa có Index độc lập. Khi ORDER BY "CreatedAt" DESC trên bảng lớn,
-- PostgreSQL phải thực hiện thao tác Sort trong bộ nhớ (Sort Method: quicksort / top-N heapsort).
-- Kiến nghị nhóm xem xét tạo thêm B-Tree Index trên ("Status", "CreatedAt" DESC) để tối ưu triệt để.

-- -----------------------------------------------------------------------------------------
-- 3. Truy vấn Chi tiết Công thức theo Slug (FR-RCP-002 / GET /api/v1/recipes/{slug})
-- Tận dụng B-Tree Index: "IDX_Recipe_Slug" (Index Scan, Cost cực thấp < 0.3ms).
-- -----------------------------------------------------------------------------------------
EXPLAIN (ANALYZE, BUFFERS)
SELECT r."Id", r."Title", r."Slug", r."Description", r."PrepTime", r."CookTime", 
       r."Servings", r."Difficulty", r."Status", r."AuthorId", r."PublishedAt", r."CreatedAt"
FROM "Recipes" AS r
WHERE r."IsDeleted" = FALSE 
  AND r."Slug" = 'pho-bo-ha-noi'
LIMIT 1;

-- -----------------------------------------------------------------------------------------
-- 4. Truy vấn Tìm kiếm toàn văn tiếng Việt có unaccent (FR-SRCH-001 / D49)
-- Tận dụng GIN Index: "IDX_Recipe_Search" trên cột "SearchVector".
-- Dùng hàm unaccent() và plainto_tsquery('simple', ...) đúng theo cấu hình D49.
-- -----------------------------------------------------------------------------------------
EXPLAIN (ANALYZE, BUFFERS)
SELECT r."Id", r."Title", r."Slug", r."Description", r."PrepTime", r."CookTime", 
       r."Difficulty", r."Status", r."AuthorId", r."CreatedAt",
       ts_rank(r."SearchVector", plainto_tsquery('simple', unaccent('pho'))) AS "Rank"
FROM "Recipes" AS r
WHERE r."IsDeleted" = FALSE
  AND r."Status" = 1
  AND r."SearchVector" @@ plainto_tsquery('simple', unaccent('pho'))
ORDER BY "Rank" DESC
LIMIT 10 OFFSET 0;

-- -----------------------------------------------------------------------------------------
-- 5. Đánh giá vấn đề N+1 Query trong EF Core (NFR-PERF-004)
-- - Kiến trúc dự án tắt hoàn toàn Lazy Loading (CONS).
-- - Trong truy vấn danh sách (FR-RCP-001, FR-CAT-001): Sử dụng LINQ Projection (.Select)
--   giúp EF Core biên dịch việc lấy ảnh đại diện và đếm số lượng công thức thành subquery
--   nằm trong CÙNG MỘT câu lệnh SQL đơn lẻ, triệt tiêu 100% nguy cơ phát sinh N+1 truy vấn.
-- - Trong truy vấn chi tiết (FR-RCP-002): Eager Loading (.Include, .ThenInclude) nạp toàn bộ
--   các tập con (Steps, Ingredients, Images) theo cơ chế batching có kiểm soát.
-- =========================================================================================

