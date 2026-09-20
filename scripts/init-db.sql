-- Chạy tự động lần đầu khi volume postgres còn rỗng.
-- FR-SRCH-001 cần unaccent (bỏ dấu tiếng Việt) và pg_trgm (tìm gần đúng).
CREATE EXTENSION IF NOT EXISTS unaccent;
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- TODO(S10 — B): tạo text search configuration cho tiếng Việt.
-- PostgreSQL KHÔNG có config "vietnamese" sẵn — phải tự dựng từ unaccent + simple.
-- Đây là rủi ro kỹ thuật lớn nhất của slice S10, thử nghiệm bằng SQL thuần từ tuần 4.
--
--   CREATE TEXT SEARCH CONFIGURATION vietnamese (COPY = simple);
--   ALTER TEXT SEARCH CONFIGURATION vietnamese
--     ALTER MAPPING FOR hword, hword_part, word WITH unaccent, simple;
