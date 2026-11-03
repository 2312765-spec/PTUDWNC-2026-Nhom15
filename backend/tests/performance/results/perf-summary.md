# Kết quả đo hiệu năng — NFR-PERF-001..004

Đo ngày **2026-11-03**. Số liệu dưới đây lấy từ output gốc trong thư mục này — không chỉnh tay.

## Môi trường

| Thành phần | Cấu hình |
|---|---|
| Máy chủ | Windows 11, 16 luồng CPU, 7,7 GB RAM, Docker Desktop 4.68 (WSL2) |
| API | Image build từ `backend/CulinaryBlog.API/Dockerfile`, **giới hạn 2 vCPU / 4 GB** (`cpus: 2`, `mem_limit: 4g`), `ASPNETCORE_ENVIRONMENT=Development` (log Debug — thực tế production còn nhanh hơn) |
| PostgreSQL 16 / Redis 7 | Container `cb-postgres`, `cb-redis` của `docker-compose.yml`, **không giới hạn CPU** |
| Dữ liệu | DbSeeder + `seed-perf-data.sql`: **10.050 recipes** (8.017 Published), 10.050 ảnh, 8 categories |
| k6 | `grafana/k6:1.0.0` chạy trong container trên cùng máy |
| Kịch bản | Mỗi lượt: `GET /categories` → `GET /recipes?page=1..5` → `GET /recipes/{slug}` → `GET /recipes/search?q=…` (`common.js`) |

## NFR-PERF-001 / 002 — Thời gian phản hồi và tải đồng thời

| Kịch bản | VU | Request | Thông lượng | p50 | p95 | p99 | Lỗi | Ngưỡng |
|---|---|---|---|---|---|---|---|---|
| Smoke (cache **trống** lúc đầu) | 5 | 302 | 9,7 req/s | 8,0 ms | 165,5 ms | **13,9 s** | 0% | ❌ p99 |
| Load (cache nóng) | 20 → **100** (giữ 3 phút) | 88.777 | 295 req/s | 8,4 ms | 39,7 ms | 100,9 ms | 0% | ✅ |
| Stress | 50 → **180** | 276.265 | 575 req/s | 22,7 ms | 212,5 ms | 424,8 ms | 0% | ✅ (p99 ≤ 2 s) |

p95 từng endpoint trong load test: categories 37,5 ms · recipes 40,9 ms · detail 39,4 ms · search 41,0 ms.
CPU của API khi stress đạt ~210% (bão hòa 2 vCPU), RAM tối đa ~515 MiB (`stress-docker-stats.txt`).

**Phát hiện — cache stampede ở `/categories`:** khi cache trống, 5 request đồng thời cùng chạy query
categories (xem NFR-PERF-004 mục 1), p95 của endpoint này lên **13,83 s** (`smoke-output.txt`).
Sau khi cache nóng (TTL 30 phút) median còn 7,5 ms. Mỗi lần cache hết hạn hoặc bị invalidate
(mọi Command trên Category/Recipe, D8) sẽ lặp lại.

Output gốc: `smoke-output.txt`, `load-output.txt`, `stress-output.txt`, `*-summary.json`.

## NFR-PERF-003 — Hiệu quả Redis cache

Bộ đếm được reset sau smoke test (làm nóng), đo trong suốt lượt load test:

| Keyspace hits | Keyspace misses | Hit rate | Ngưỡng |
|---|---|---|---|
| 88.636 | 141 | **99,84%** | ✅ ≥ 80% |

Output gốc: `redis-cache-output.txt`.

## NFR-PERF-004 — EXPLAIN ANALYZE (SQL thật do EF Core sinh ra, 10.050 recipes)

| # | Query | Kế hoạch | Thời gian | Đánh giá |
|---|---|---|---|---|
| 1 | `GET /categories` (Include Recipes + Images) | Seq Scan Recipes + RecipeImages, nạp **8.017 dòng** chỉ để đếm `recipeCount`; Sort **tràn đĩa** (external merge 6,9 MB) | **532,6 ms** | ❌ > 100 ms |
| 2a | `GET /recipes` — COUNT | Seq Scan Recipes | 6,4 ms | ✅ |
| 2b | `GET /recipes` — trang, `ORDER BY "CreatedAt" DESC` | Seq Scan + top-N heapsort — **không có index trên `CreatedAt`** | 7,0 ms | ⚠️ tăng tuyến tính theo số recipe |
| 3 | `GET /recipes/{slug}` | Index Scan `IX_Recipes_Slug` | 0,8 ms | ✅ |
| 4a | Search — COUNT | Bitmap Index Scan GIN `IX_Recipes_SearchVector` | 2,0 ms | ✅ |
| 4b | Search — `ts_rank` + phân trang | Bitmap Index Scan GIN `IX_Recipes_SearchVector` | 3,6 ms | ✅ |

**N+1:** không có. Mỗi endpoint chạy số câu SQL cố định (log EF Core): categories 1, list 2, search 4
(count, rank, nạp theo id, tên tác giả), detail 6 (AsSplitQuery: chính + steps + ingredients + images,
cộng 2 câu hồ sơ tác giả).

Output gốc: `explain-analyze-output.txt`.

## Việc cần làm (ngoài phạm vi PR đo hiệu năng)

1. **FR-CAT-001:** đếm `recipeCount` bằng projection `Count()` trong SQL thay vì Include toàn bộ
   recipe + ảnh — sửa cả query 532 ms lẫn cache stampede.
2. **NFR-PERF-004:** migration thêm index `("Status", "CreatedAt" DESC)` cho `Recipes`
   (SRS: mọi cột ORDER BY phải có index).
3. **NFR-PERF-004:** cảnh báo slow query > 100 ms (Serilog) — chưa có trong code.
4. **NFR-PERF-001:** p95 ≤ 500 ms cho write operations — chưa đo (cần kịch bản k6 có đăng nhập).
5. **NFR-PERF-002:** lần đo này chỉ giới hạn API ở 2 vCPU / 4 GB; PostgreSQL và Redis chạy ngoài giới hạn.
