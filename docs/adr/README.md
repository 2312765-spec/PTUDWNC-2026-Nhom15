# Architecture Decision Records

Mỗi quyết định **kiến trúc** quan trọng = 1 file `NNNN-tieu-de.md`.

> **Phân biệt với `docs/decisions.md`:**
> `decisions.md` chốt các chỗ **SRS tự mâu thuẫn hoặc thiếu** (D1–D22) — đó là làm rõ
> yêu cầu, không phải chọn kiến trúc.
> `adr/` dành cho quyết định **kỹ thuật** phát sinh trong lúc code, mà SRS không nói gì.

## Mẫu

```markdown
# ADR-0001: <Tiêu đề>

- **Ngày:** YYYY-MM-DD
- **Trạng thái:** Đề xuất | Đã chấp nhận | Đã thay thế bởi ADR-XXXX
- **Liên quan:** FR-xxx, NFR-xxx, decisions.md D-x

## Bối cảnh
Vấn đề là gì, SRS nói gì, ràng buộc nào chi phối.

## Quyết định
Chọn phương án nào.

## Phương án đã cân nhắc
1. ... — ưu / nhược
2. ... — ưu / nhược

## Hệ quả
Được gì, mất gì, phải sửa gì về sau.
```

## ADR cần viết

| ADR | Chủ đề | Nguồn | Khi nào |
|---|---|---|---|
| 0001 | Bucket MinIO `public-read` và ảnh của recipe Draft | D16 | S8 |
| 0002 | Chiến lược invalidate cache theo tag | D8 | S3 |

> Các mâu thuẫn SRS còn lại (D1–D15, D17–D22) **không cần ADR riêng** — lý do và hệ quả
> đã ghi đầy đủ trong `decisions.md`. Chỉ hai mục trên là quyết định kiến trúc thật sự,
> có đánh đổi kỹ thuật cần giải thích.
>
> Thêm ADR mới khi gặp quyết định kỹ thuật lớn trong lúc code: chọn thư viện resize ảnh,
> cách cấu hình text search tiếng Việt cho PostgreSQL, chiến lược migration khi đổi schema…
