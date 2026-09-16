# Application/Recipes/Commands/Images — người phụ trách: **D**

FR-RCP-008, FR-FILE-001, FR-FILE-002. Slice S8.

**D22 — BA endpoint, không phải bốn:**

| Method | Endpoint | Body |
|---|---|---|
| POST | `/recipes/{id}/images` | multipart: `file`, `altText?` |
| PATCH | `/recipes/{id}/images/{imageId}` | `{ altText?, isPrimary?, orderIndex? }` |
| DELETE | `/recipes/{id}/images/{imageId}` | — |

**KHÔNG làm** `/images/{imageId}/primary` như SRS FR-RCP-008 viết.

**Thứ tự validation (NFR-SEC-004) — sai thứ tự là lỗ hổng DoS:**
1. Kiểm tra size **trước khi đọc stream** (≤ 5MB)
2. MIME type
3. **Magic bytes** — JPEG `FF D8 FF`, PNG `89 50 4E 47`. Không tin Content-Type header.
4. Tên file sinh bằng **GUID**, không dùng tên người dùng gửi lên.

**D1:** xóa recipe **không** xóa file MinIO. FR-FILE-002 chỉ chạy khi xóa một ảnh cụ thể.
