# ADR-0001: Bucket MinIO `public-read` và ảnh của recipe Draft

- **Ngày:** 2026-09-20
- **Trạng thái:** Đã chấp nhận
- **Liên quan:** FR-RCP-008, FR-FILE-001, NFR-SEC-004, decisions.md D16

## Bối cảnh

Ảnh công thức lưu trên MinIO bucket `culinary-blog` (FR-FILE-001). SRS ghi policy `public-read`
nhưng đồng thời yêu cầu ảnh của recipe Draft không được lộ. Hai điều này mâu thuẫn:
với `public-read`, ai có URL đều xem được ảnh, kể cả khi recipe chưa Publish.

## Quyết định

Giữ bucket `public-read` (D16). Chấp nhận việc ảnh của recipe Draft truy cập được bằng URL
trực tiếp. Mức giảm thiểu:

- Tên object là `recipes/{recipeId}/{Guid v4}{ext}`, không đoán được trong thực tế.
- Tên do server sinh, không dùng tên file client gửi (NFR-SEC-004).
- Không liệt kê được nội dung bucket (không cấp quyền `s3:ListBucket` cho anonymous).

## Phương án đã cân nhắc

1. **Presigned URL cho mọi ảnh** — kín hơn, nhưng URL hết hạn nên frontend phải refresh, làm hỏng
   ISR/cache của trang recipe (`/recipes/[slug]` ISR 300) và Open Graph image.
2. **Bucket private + API proxy ảnh** — kín nhất, nhưng toàn bộ lưu lượng ảnh đi qua API,
   mất lợi ích của `next/image` và CDN/Nginx cache.
3. **Hai bucket (public / draft)** — phải di chuyển object khi Publish/Unpublish, thêm điểm lỗi.

## Hệ quả

- **Rủi ro chấp nhận:** nếu ai đó có URL ảnh của recipe Draft (ví dụ Author tự chia sẻ nhầm) thì xem
  được ảnh đó dù chưa Publish. Không lộ dữ liệu văn bản của recipe.
- Frontend dùng URL ảnh trực tiếp, không cần cơ chế làm mới URL.
- Nếu sau này cần bảo mật ảnh Draft, chuyển sang phương án 1 hoặc 2 và viết ADR thay thế.
- Bucket và policy `public-read` được tạo bởi service `minio-init` trong `docker-compose.yml`
  (chạy `mc mb`/`mc anonymous set download` một lần lúc container khởi động), **không phải**
  do `MinioFileStorageService` tự tạo lúc chạy — service chỉ giả định bucket đã tồn tại
  (`Minio:BucketName` trong cấu hình). Production cần chạy tương đương `minio-init` (hoặc IaC)
  trước khi deploy API.
