# Plan — CONS-009: S3 production chuyển từ MinIO sang RustFS

> Kế hoạch viết **trước khi code** (bước Plan, CLAUDE.md mục 9), được duyệt 2026-11-05.
> Người phụ trách: D (Media & Vận hành) — 2312765@dlu.edu.vn.
> Branch: `chore/CONS-009-prod-s3` (tách từ `chore/CONS-009-dev-s3mock` — cần D65).

---

## 1. Vấn đề (D65.3)

Mọi image MinIO đã bị gỡ khỏi registry công khai, **kể cả digest prod đang ghim**
(`quay.io/minio/minio@sha256:14cea4…` → "no such manifest", kiểm 2026-11-05). Server mới không dựng
được `docker-compose.prod.yml`; cách `docker load` hiện tại chỉ chạy khi còn máy giữ cache image.

**Hiện trạng prod:** chưa có server production thật (kiểm 2026-11-05: `.env.production` duy nhất là
`culinary.localhost`, không có workflow deploy, `culinaryblog.vn` không phân giải DNS) → không cần chuyển
dữ liệu thật; `deployment.md` chỉ ghi ngắn cách chuyển nếu sau này cần.

## 2. Chọn S3 thay thế

| Ứng viên | Đánh giá |
|---|---|
| **RustFS** `1.0.3` (17/10/2026) | **Chọn.** Bản 1.0 chính thức, thiết kế thay thẳng MinIO (S3 API, bucket policy, `/health`). Image có `curl`; chạy user `rustfs`, `/data` có sẵn thuộc user đó |
| SeaweedFS | Lâu đời, nhưng quyền đọc ẩn danh cấu hình bằng file identity riêng — khác hẳn MinIO, đổi nhiều hơn |
| `adobe/s3mock` | Không phân quyền, không thiết kế cho dữ liệu thật — chỉ dev/test (D65) |

**Thử RustFS 1.0.3 + `amazon/aws-cli:2.38.0` (13/10/2026) ngày 2026-11-05:**

| Kiểm tra | Kết quả |
|---|---|
| `create-bucket` + `put-bucket-policy` (chỉ `s3:GetObject` cho `*`) | OK |
| PUT có xác thực | OK |
| GET ẩn danh | 200 `image/png` |
| PUT ẩn danh | **403** |
| LIST bucket ẩn danh | **403** |
| Object sau restart (volume) | 200 |
| `/health` | 200 |

## 3. Quyết định mới — D66

Prod dùng RustFS thay MinIO; bucket + policy do `amazon/aws-cli` tạo (thay `mc`, cũng bị khóa).

## 4. File sửa

| File | Thay đổi |
|---|---|
| `docker-compose.prod.yml` | Service giữ tên `minio` (cổng 9000) — `nginx.prod.conf`, `Minio__Endpoint` không đổi. Image `${S3_IMAGE:-rustfs/rustfs:1.0.3}`; khóa từ `MINIO_ROOT_USER`/`MINIO_ROOT_PASSWORD` sẵn có. Volume `s3data`. Healthcheck `curl /health`. `minio-init` → `amazon/aws-cli:2.38.0`: tạo bucket + policy chỉ GET ẩn danh (D16) |
| `scripts/verify-prod.sh` | Bước 7 đổi `mc` → `aws-cli`; thêm: PUT ẩn danh và LIST ẩn danh qua Nginx phải bị chặn |
| `.env.production.example` | Bỏ `MINIO_IMAGE`/`MC_IMAGE`, thêm `S3_IMAGE` tùy chọn |
| `docs/deployment.md` | Viết lại mục 3; đoạn ngắn chuyển dữ liệu từ volume MinIO cũ |
| `docs/decisions.md` | D66; đóng D65.3 |
| `docs/traceability.md`, `CHANGELOG.md` | Ghi chú CONS-009 |

## 5. Kiểm tra

1. `docker compose -f docker-compose.prod.yml config` hợp lệ.
2. Dựng stack prod trên máy (`culinary.localhost`, cert tự ký) → mọi service healthy.
3. `scripts/verify-prod.sh` pass toàn bộ.
4. API `/health` Healthy (gồm S3).
5. Upload ảnh qua API thật → `MinioFileStorageService` ghi vào RustFS, GET qua Nginx 200.

## 6. Kết quả (2026-11-05, máy local, `culinary.localhost`)

| Bước | Kết quả |
|---|---|
| `compose config` | Hợp lệ |
| `up -d --build --wait` | Mọi service healthy; `minio-init` (aws-cli) exit 0 |
| `verify-prod.sh` 1–8 | **Đạt** — gồm GET ảnh 200, PUT ẩn danh 403, **LIST ẩn danh 403** (mới), next/image 200, `/sitemap.xml` 200 |
| `verify-prod.sh` 9 | ✗ "file log lạ" — log 21/10, 28/10, 03/11 nằm trong volume `apilogs` dùng lại từ lần chạy trước (image build 05/11 14:06, không chứa log). Không liên quan S3; hạn chế của bước kiểm tra |
| API `/health` (gồm S3) | Healthy |
| SDK thật của API | Job sitemap (`GenerateSitemapCommand`) ghi `seo/sitemap.xml` vào RustFS qua `MinioFileStorageService`; Nginx đọc lại 200 |

Upload ảnh qua endpoint FR-RCP-008 chưa kiểm được trên prod: cần recipe, mà FR-RCP-003 (tạo recipe) chưa
có và prod không có seed — giống ghi chú sẵn trong `verify-prod.sh` bước 7.
