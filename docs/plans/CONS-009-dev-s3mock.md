# Plan — CONS-009: S3 cho môi trường dev/CI khi MinIO khóa image

> Kế hoạch viết **trước khi code** (bước Plan, CLAUDE.md mục 9), được duyệt 2026-11-05.
> Người phụ trách: D (Media & Vận hành) — 2312765@dlu.edu.vn.
> Branch: `chore/CONS-009-dev-s3mock`.

---

## 1. Vấn đề

`docker compose up -d` thất bại trên máy chưa có cache image: MinIO đã khóa phân phối image công khai.
Kiểm bằng `docker manifest inspect` (hỏi thẳng registry, không dùng cache) ngày 2026-11-05:

| Image | Kết quả |
|---|---|
| `quay.io/minio/minio:latest` | no such manifest |
| `quay.io/minio/minio@sha256:14cea4…` (digest prod) | no such manifest |
| `minio/minio:latest` (Docker Hub) | unauthorized |
| `quay.io/minio/mc:latest` | no such manifest |
| `adobe/s3mock:5.2.3` · `rustfs/rustfs` · `chrislusf/seaweedfs` | OK |

**Phạm vi:** chỉ dev (`docker-compose.yml`) và CI. **Ngoài phạm vi:** `docker-compose.prod.yml` — prod giữ
cơ chế `MINIO_IMAGE`/`docker load` hiện có, quyết định thay thế làm việc riêng (D65 ghi rủi ro).

## 2. Quyết định mới — D65

Dev + CI dùng `adobe/s3mock:5.2.3` thay MinIO. Đã dùng sẵn trong integration test
(`IntegrationTests/Common/S3MockContainer.cs`) và job Lighthouse CI — dev/test/CI cùng một S3.

## 3. Kiểm chứng s3mock 5.2.3 trước khi đổi

- PUT + GET không xác thực → 200, giữ đúng `Content-Type` (ảnh public như D16).
- `COM_ADOBE_TESTING_S3MOCK_STORE_ROOT` + `..._RETAIN_FILES_ON_EXIT=true` + volume → object còn sau restart.
- Image có `wget` → healthcheck.
- API không gọi lệnh cấp bucket (policy/create) — bucket tạo bằng `..._INITIAL_BUCKETS`.

## 4. File sửa

| File | Thay đổi |
|---|---|
| `docker-compose.yml` | Service **giữ tên `minio`**, cổng host 9000 → container 9090 (cấu hình `localhost:9000` không đổi). Volume mới `s3mockdata`. Healthcheck `wget`. Bỏ `minio-init` và console 9001 |
| `.github/workflows/ci.yml` | Job `lighthouse` dùng lại `docker compose up -d --wait postgres redis minio` |
| `docs/decisions.md` | D65 |
| `CLAUDE.md` | Dòng cổng + chú thích lệnh `docker compose up` |
| `docs/traceability.md` | Ghi chú FR-FILE ("lưu ý cho D") + CONS-009 |

## 5. Kiểm tra

1. Dừng MinIO cũ, `docker compose up -d` → mọi service healthy.
2. API Development: `/health` Healthy (gồm S3).
3. PUT ảnh vào bucket → lấy qua `http://localhost:3000/_next/image?...` (next/image, D58).
4. `dotnet test` lọc `MinioFileStorageServiceTests`.

### Kết quả (2026-11-05, máy local)

| Bước | Kết quả |
|---|---|
| `docker compose config` + `up -d --wait` | Hợp lệ; cả 5 service healthy |
| PUT PNG → restart S3 → GET | 200 `image/png`, object còn sau restart |
| API Development `/health` (Postgres + Redis + S3) | `Healthy` |
| `next/image` lấy ảnh S3 qua `localhost:3000/_next/image` | 200 `image/png` |
| `MinioFileStorageServiceTests` | 9/9 pass |

**Lỗi gặp khi làm:** gắn volume vào `/data` → S3 trả 500, healthcheck unhealthy. s3mock chạy bằng
user `cnb` (uid 1000) nhưng volume mới gắn vào thư mục không có trong image sẽ thuộc `root`. Dùng
`/s3mockroot` (có sẵn trong image, thuộc `cnb`) — volume mới thừa hưởng quyền đó.

## 6. Lưu ý

- Ảnh trong volume `miniodata` cũ không chuyển sang (định dạng khác). Seed dùng `placehold.co` → không ảnh hưởng.
- Service `api` profile `full` chưa có cấu hình S3 (lỗi có từ trước, ngoài phạm vi).
