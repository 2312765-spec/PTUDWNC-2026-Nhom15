# Plan — D34: RowVersion thật sự hoạt động trên PostgreSQL (optimistic concurrency)

> Người phụ trách: D (Media & Vận hành) — 2312765@dlu.edu.vn. Viết **trước khi code**
> (vòng lặp CLAUDE.md mục 9), branch `fix/D34-rowversion-concurrency`.

---

## 1. Vấn đề

SRS 7.1 mô tả `RowVersion` là "bytea (timestamp), EF Core [Timestamp]". Cơ chế `[Timestamp]` chỉ
tự chạy trên **SQL Server** (DB tự tăng `rowversion` sau mỗi lần ghi). **PostgreSQL không có cơ chế
đó** — cột `bytea` giữ nguyên giá trị rỗng mãi mãi nếu code không tự đổi.

Hệ quả: EF sinh `UPDATE ... WHERE "RowVersion" = ''` — điều kiện **luôn đúng**. Hai người cùng sửa
một recipe → người lưu sau âm thầm ghi đè người lưu trước, không bao giờ có 409
`RECIPE_CONCURRENCY_CONFLICT` (D4). Đây là lost update SRS mục 3.x (Recipe module) yêu cầu chặn.

Phát hiện kèm: `AuditInterceptor` (tự set `CreatedAt`/`UpdatedAt`, SRS 7.1) đã viết nhưng **chưa
bao giờ được đăng ký** vào DbContext.

## 2. Quyết định áp dụng

| D-x | Nội dung | Ảnh hưởng |
|---|---|---|
| **D34 (mới)** | Trên PostgreSQL, `RowVersion` do **code sinh**: mỗi lần SaveChanges, entity `Added`/`Modified` nhận 16 byte ngẫu nhiên mới. Giữ cột `bytea` như SRS, không dùng `xmin` | Không migration, không đổi schema |
| D4 | Mismatch → 409 `RECIPE_CONCURRENCY_CONFLICT` | `GlobalExceptionMiddleware` đã map sẵn `DbUpdateConcurrencyException` |
| D31 | Child entity phát hiện qua navigation fixup bị EF đoán nhầm là `Modified` | Interceptor KHÔNG được làm hỏng pattern `AddAsync` tường minh của D31 |

## 3. File sẽ tạo / sửa

| Tầng | File | Thay đổi |
|---|---|---|
| Docs | `docs/decisions.md` | Thêm D34 + dòng tra nhanh ở bảng đầu file |
| Infrastructure | `Persistence/RowVersionInterceptor.cs` (mới) | `SavingChanges(Async)`: gán `RowVersion` mới cho `BaseEntity` `Added`/`Modified`; nếu chỉ owned `Nutrition` đổi thì cũng đổi `RowVersion` của Recipe chủ |
| Infrastructure | `DependencyInjection.cs` | Đăng ký `AuditInterceptor` + `RowVersionInterceptor` qua `AddInterceptors` |
| Test | `IntegrationTests/Common/ConcurrencyTests.cs` (mới) | Xem mục 4 |

**Không đụng:** Domain (không thêm phụ thuộc), Application, schema/migration.

## 4. Test (viết trước, chạy xác nhận fail)

1. Hai DbContext cùng đọc một recipe → cả hai sửa → người lưu thứ hai nhận `DbUpdateConcurrencyException`.
2. `RowVersion` khác rỗng sau khi tạo, và đổi sau mỗi lần lưu.
3. Chỉ sửa `Nutrition` (owned) → `RowVersion` của Recipe vẫn đổi.
4. `CreatedAt`/`UpdatedAt` được interceptor điền (AuditInterceptor).
5. Hồi quy D31: thêm ảnh vào recipe đã có qua `ImagesTests` hiện có — vẫn pass.

## 5. Hướng dẫn cho FR-RCP-004 (B/C — chưa làm trong PR này)

`PUT /api/v1/recipes/{id}` nhận `RowVersion` client đang giữ (header `If-Match`, base64). Handler:

1. Load recipe, so `recipe.RowVersion` với giá trị client gửi — khác → `ConflictException(RECIPE_CONCURRENCY_CONFLICT)` → 409 (client đang sửa trên bản cũ).
2. Mutate + `SaveChangesAsync` — nếu có request khác chen vào giữa bước 1 và 2, `RowVersionInterceptor` + concurrency token đảm bảo EF ném `DbUpdateConcurrencyException` → 409.
3. Response trả `RowVersion` mới (header `ETag`) để client dùng cho lần sửa tiếp.

## 6. Xong khi

Test mục 4 pass · toàn bộ test cũ pass · 0 warning · `decisions.md` có D34 · `SchemaDriftTests` vẫn xanh (không đổi schema).
