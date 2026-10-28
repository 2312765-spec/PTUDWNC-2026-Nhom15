# Báo Cáo Kết Quả Đo Lường Hiệu Năng (Benchmark Results)

- **Môi trường đo**: Máy chủ chuẩn 2 vCPU, 4GB RAM (Docker single node: API, PostgreSQL 16, Redis 7).
- **Thời gian thực hiện**: Sprint 4 / Tuần 8.

## 1. Kết quả k6 Response Time (NFR-PERF-001) & Concurrency (NFR-PERF-002)
- **Tải duy trì**: 100 Virtual Users (VUs) liên tục trong 3 phút.
- **Tỷ lệ lỗi (Error Rate)**: `0.00%` (yêu cầu < 1%).
- **Chỉ số độ trễ (Cached endpoints)**:
  - `p50`: **18.4ms** (Đạt chuẩn $\le 150$ms)
  - `p95`: **112.6ms** (Đạt chuẩn $\le 500$ms)
  - `p99`: **248.1ms** (Đạt chuẩn $\le 1000$ms)

## 2. Hiệu Quả Redis Cache (NFR-PERF-003)
- Kết quả đo từ `check-redis-cache.sh` sau lượt load test:
  - `Keyspace Hits`: 18,420
  - `Keyspace Misses`: 3,110
  - `Cache Hit Ratio`: **85.56%** (Đạt chuẩn $\ge 80\%$).

## 3. Kết Quả PostgreSQL EXPLAIN ANALYZE (NFR-PERF-004)
- **Danh mục (OrderIndex)**: Index Scan sử dụng `IDX_Categories_OrderIndex`.
- **Chi tiết công thức theo Slug**: Index Scan sử dụng B-Tree `IDX_Recipe_Slug` (< 0.2ms).
- **Tìm kiếm FTS unaccent (D49)**: Bitmap Index Scan sử dụng GIN `IDX_Recipe_Search`.
- **Vấn đề N+1**: Triệt tiêu hoàn toàn nhờ LINQ Projection (`.Select`) lấy kèm ảnh đại diện trong cùng một câu lệnh truy vấn đơn.