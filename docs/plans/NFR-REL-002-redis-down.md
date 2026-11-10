# Kế hoạch — NFR-REL-002: Redis down không làm sập request

**Ngày:** 2026-11-10 · **Branch:** `feature/NFR-REL-002-redis-down`
**Phạm vi:** phần Redis của NFR-REL-002 (SRS: "Redis down → fallback database, không throw").
Chỉ backend. Không có FR mới. Quyết định áp dụng: **D8**; quyết định mới: **D90**.

## 1. Hiện trạng

| Thành phần | Hiện trạng |
|---|---|
| `CachingBehavior` | try/catch quanh GET/SET cache → lỗi thì gọi handler |
| `CacheInvalidationBehavior` | try/catch quanh xoá tag → lỗi thì log Warning, response vẫn trả |
| `RedisCacheService` | Nuốt mọi lỗi Redis, log Warning |
| `DependencyInjection` | `AbortOnConnectFail = false` — API khởi động được khi Redis tắt |
| Test | **Chưa có test nào tắt Redis giữa chừng.** Integration test đều chạy với Redis sống (Testcontainers) |

**Nghi ngờ 1 — request treo:** khi mất kết nối, StackExchange.Redis 2.8 mặc định xếp lệnh vào
backlog chờ kết nối lại, tới `AsyncTimeout` (5 s) mới báo lỗi. Request không sập nhưng chậm ~5 s
mỗi lần đọc cache.

**Nghi ngờ 2 — cache cũ sau khi Redis lên lại:** ghi dữ liệu lúc Redis tắt → xoá tag thất bại.
Redis lên lại (dữ liệu RDB còn) → trả bản cũ tới hết TTL (`categories:all` tối đa 30 phút).

## 2. Test trước

`backend/tests/CulinaryBlog.IntegrationTests/Resilience/RedisDownTests.cs` + factory
`Resilience/Support/RedisChaosApiFactory.cs` (Postgres + Redis Testcontainers; Redis gắn vào
**cổng host cố định** để dừng rồi chạy lại mà API vẫn trỏ đúng địa chỉ).

| # | Kịch bản | Kỳ vọng |
|---|---|---|
| 1 | Tắt Redis → `GET /categories`, `/recipes`, `/recipes/{slug}`, `/search?q=` | 200, dữ liệu từ DB, mỗi request < 1 s |
| 2 | Tắt Redis → Admin `POST /categories` | 201, danh mục có trong DB |
| 3 | Tắt Redis → `/health/live`, `/health/ready` | 200 · 503 (readiness gồm Redis — FR-OBS-001) |
| 4 | Nạp cache → tắt Redis → tạo danh mục → bật Redis | `GET /categories` thấy danh mục mới (không trả bản cũ) |
| 5 | Bật Redis lại | Cache hoạt động tiếp (key `categories:all` có trong Redis), không cần khởi động lại API |

## 3. Code

| Tầng | File | Thay đổi |
|---|---|---|
| Infrastructure | `DependencyInjection.cs` | `BacklogPolicy.FailFast` + timeout ngắn cho lệnh cache (nếu test 1 xác nhận nghi ngờ 1) |
| Infrastructure | `Caching/RedisCacheRecovery.cs` (mới) | Đăng ký `ConnectionRestored` (kết nối Interactive): SCAN `tag:*` → xoá mọi key cache + tag set. Không giữ trạng thái trong bộ nhớ (D8, NFR-SCALE-001) |
| Unit | `UnitTests/...` nếu cần | — |

Tầng Application không đổi.

## 4. Tài liệu

- `docs/decisions.md`: **D90** — timeout cache, FailFast, xoá toàn bộ cache khi kết nối lại.
- `docs/traceability.md`: NFR-REL-002 ⬜ → trạng thái theo kết quả test.

## 5. Kết quả (2026-11-10)

| | Trước khi sửa | Sau khi sửa |
|---|---|---|
| Test 1 (GET khi Redis tắt) | ❌ `GET /categories` 5,9 s | ✅ |
| Test 2 (tạo danh mục khi Redis tắt) | ❌ 201 nhưng 5,4 s | ✅ |
| Test 3 (health) | ✅ | ✅ |
| Test 4 (cache cũ) | ❌ trả danh sách rỗng đã cache | ✅ |
| Test 5 (cache hoạt động lại) | ✅ | ✅ |

Phát sinh: đặt `ConnectTimeout = 1000` làm multiplexer không kết nối được Redis cả khi Redis đang chạy (Docker
Desktop/Windows) → giữ mặc định 5 s. Chi tiết và hạn chế: D90.

## 6. Ngoài phạm vi

Postgres down (SRS: "tự reconnect, timeout 30s") — Npgsql mặc định; Hangfire retry 3× — đã có ở
FR-JOB (D42). Circuit Breaker Polly — SRS ghi "tùy chọn".
