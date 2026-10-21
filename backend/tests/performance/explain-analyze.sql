/*
Thực thi EXPLAIN ANALYZE cho các câu lệnh truy 
vấn chính của Người B (Categories, Recipes, Search) 
trong PostgreSQL, chứng minh không bị N+1 và sử dụng Index hiệu quả (đáp ứng NFR-PERF-004)*/

-- ==============================================================================
-- NFR-PERF-004: KIỂM TRA HIỆU NĂNG QUERY, INDEX VÀ CHỐNG N+1
-- Hệ quản trị CSDL: PostgreSQL
-- Người thực hiện: B (Khám phá & Tìm kiếm)
-- ==============================================================================

-- 1. KIỂM TRA QUERY DANH MỤC (FR-CAT-001)
-- Kỳ vọng: Dùng Index Scan / Bitmap Index scan trên Slug hoặc Id, thời gian thực thi < 2ms
EXPLAIN ANALYZE
SELECT "c"."Id", "c"."Name", "c"."Slug", "c"."DisplayOrder", "c"."RecipeCount"
FROM "Categories" AS "c"
ORDER BY "c"."DisplayOrder" ASC;

-- 2. KIỂM TRA QUERY LỌC RECIPES THEO TRẠNG THÁI & DANH MỤC CÓ PHÂN TRANG (FR-RCP-001)
-- Kỳ vọng: Dùng Index "IX_Recipes_CategoryId", "IX_Recipes_Status", "IX_Recipes_CreatedAt"
-- KHÔNG BỊ N+1: EF Core dùng phép chiếu Select(...) sang RecipeSummaryDto trong 1 câu SQL duy nhất
EXPLAIN ANALYZE
SELECT 
    "r"."Id", 
    "r"."Title", 
    "r"."Slug", 
    "r"."Description", 
    "r"."CoverImageUrl", 
    "r"."CookingTimeMinutes", 
    "r"."Difficulty", 
    "r"."CategoryId", 
    "c"."Name" AS "CategoryName", 
    "r"."AuthorId", 
    "r"."CreatedAt"
FROM "Recipes" AS "r"
INNER JOIN "Categories" AS "c" ON "r"."CategoryId" = "c"."Id"
WHERE "r"."Status" = 'Published' AND "r"."IsDeleted" = FALSE
ORDER BY "r"."CreatedAt" DESC
LIMIT 12 OFFSET 0;

-- 3. KIỂM TRA QUERY CHI TIẾT RECIPE THEO SLUG (FR-RCP-002)
-- Kỳ vọng: Unique Index Scan trên "IX_Recipes_Slug", cost cực thấp (< 0.1ms)
EXPLAIN ANALYZE
SELECT "r".*
FROM "Recipes" AS "r"
WHERE "r"."Slug" = 'pho-bo-ha-noi-truyen-thong' AND "r"."IsDeleted" = FALSE;

-- 4. KIỂM TRA QUERY TÌM KIẾM TOÀN VĂN TIẾNG VIỆT (FR-SRCH-001)
-- Kỳ vọng: Bitmap Index Scan trên GIN index "IX_Recipes_SearchVector", KHÔNG quét toàn bảng (Seq Scan)
EXPLAIN ANALYZE
SELECT 
    "r"."Id", 
    "r"."Title", 
    "r"."Slug", 
    "r"."Description",
    ts_rank("r"."SearchVector", to_tsquery('simple', 'pho:*')) AS "Rank"
FROM "Recipes" AS "r"
WHERE "r"."Status" = 'Published' 
  AND "r"."IsDeleted" = FALSE
  AND "r"."SearchVector" @@ to_tsquery('simple', 'pho:*')
ORDER BY "Rank" DESC
LIMIT 10 OFFSET 0;

-- 5. BÁO CÁO PHÂN TÍCH CHỐNG LỖI N+1 (NFR-PERF-004 RESOLUTION):
-- Thay vì lặp từng Recipe để gọi Author/Category (tạo ra 1 + N câu truy vấn):
--  a) CategoryName được JOIN trực tiếp trong phép chiếu LINQ: .Select(r => new RecipeSummaryDto(..., r.Category.Name, ...))
--  b) Tên Author được gom ID theo batch và truy vấn 1 lần qua IIdentityService.GetDisplayNamesAsync(authorIds),
--     đảm bảo số lượng query cố định O(1) bất kể pageSize.