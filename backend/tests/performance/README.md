# Kiểm thử hiệu năng (NFR-PERF-001..004)

| File | Mục đích |
|---|---|
| `common.js` | Lượt đọc dùng chung (categories → list → detail → search) và ngưỡng NFR-PERF-001 |
| `smoke-test.js` | 5 VU / 30 s — kiểm tra script, làm nóng cache |
| `load-test.js` | Lên 100 VU, giữ 3 phút (NFR-PERF-001 + 002) |
| `stress-test.js` | Lên 180 VU để tìm giới hạn (NFR-PERF-002) |
| `check-redis-cache.sh` | Tỷ lệ cache hit Redis (NFR-PERF-003) |
| `seed-perf-data.sql` | Sinh ~10.000 recipes cho DB dev/perf — **không chạy trên production** |
| `explain-analyze.sql` | EXPLAIN ANALYZE SQL thật do EF Core sinh ra (NFR-PERF-004) |
| `results/` | Output gốc của lần đo gần nhất + `perf-summary.md` |

## Chuẩn bị

```bash
docker compose up -d                       # postgres, redis, ...
dotnet run --project backend/CulinaryBlog.API   # Development: tự migrate + DbSeeder

docker exec -i cb-postgres psql -U postgres -d culinaryblog < backend/tests/performance/seed-perf-data.sql
docker exec cb-redis redis-cli FLUSHALL    # bỏ cache cũ sau khi đổi dữ liệu
```

Muốn đo đúng NFR-PERF-002 (2 vCPU / 4 GB) thì chạy API trong container có giới hạn, ví dụ
`cpus: 2` và `mem_limit: 4g` trong một file compose override.

## Chạy

Không cần cài k6 — dùng image `grafana/k6` (chạy từ `backend/tests/performance`):

```bash
K6="docker run --rm -v $PWD:/scripts -w /scripts -e BASE_URL=http://host.docker.internal:5000 grafana/k6:1.0.0 run"

$K6 --summary-export results/smoke-summary.json smoke-test.js    # làm nóng cache
./check-redis-cache.sh reset                                      # đếm hit/miss từ đây
$K6 --summary-export results/load-summary.json load-test.js
./check-redis-cache.sh                                            # NFR-PERF-003
$K6 --summary-export results/stress-summary.json stress-test.js

docker exec -i cb-postgres psql -U postgres -d culinaryblog < explain-analyze.sql   # NFR-PERF-004
```

Đã cài k6 thì thay `$K6` bằng `k6 run` và `BASE_URL` mặc định là `http://localhost:5000`.

Sau khi đổi LINQ trong repository, lấy lại SQL từ log `Microsoft.EntityFrameworkCore.Database.Command`
(Development) và cập nhật `explain-analyze.sql`.
