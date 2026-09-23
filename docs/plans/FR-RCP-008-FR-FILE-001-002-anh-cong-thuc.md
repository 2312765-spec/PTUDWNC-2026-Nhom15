# Plan — FR-RCP-008, FR-FILE-001, FR-FILE-002: Ảnh công thức + MinIO storage

> **Ghi chú:** file này viết **sau khi code đã chạy và test đã pass** (branch
> `2312765_NVThuan_FR-RCP-008_FR-FILE-001/002`), dựng lại theo đúng những gì đã implement —
> không phải kế hoạch viết trước khi code. Mục đích: làm tài liệu tham chiếu cho review chéo
> (mục 5 `team-assignment.md`) và cho việc nối FE sau này.
>
> Người phụ trách: D (Media & Vận hành) — 2312765@dlu.edu.vn.

---

## 1. Phạm vi

| FR | Nội dung | Trạng thái |
|---|---|---|
| FR-RCP-008 | Upload / sửa metadata (đặt primary, alt text, orderIndex) / xóa ảnh công thức | ✅ Backend xong |
| FR-FILE-001 | `IFileStorageService.UploadAsync` — MinIO, tên file GUID, chống path traversal | ✅ Xong |
| FR-FILE-002 | `IFileStorageService.DeleteAsync` — xóa file MinIO qua Hangfire fire-and-forget | ✅ Xong |

**Chưa làm:** toàn bộ Frontend (`components/upload/**`) — xem mục 6.

## 2. Quyết định D-x áp dụng (đọc trước khi sửa code này)

| D-x | Tóm tắt | Ảnh hưởng |
|---|---|---|
| D22 | 3 endpoint: `POST /images`, `PATCH /images/{imageId}` (metadata chung), `DELETE /images/{imageId}`. **Bỏ** `PATCH .../primary` riêng | Route + response shape |
| D27 | Response `POST` = `{ imageId, originalUrl, altText, isPrimary }`. Body `POST` không nhận `isPrimary` (server tự quyết). `orderIndex` = `Max hiện có + 1`. PATCH rỗng → 400. Sửa ảnh được ở **mọi status** recipe (chỉ check ownership) | Contract DTO, validator |
| D28 | Magic bytes đầy đủ 4 định dạng (JPEG/PNG/WebP/AVIF), đuôi file lấy từ định dạng phát hiện được, không lấy từ tên client gửi | `ImageSignature.cs` |
| D1 | Soft delete recipe không cascade — áp dụng tương tự: xóa `RecipeImage` không đụng `Recipe`, không cascade | `DeleteRecipeImageCommandHandler` |
| D16 | Bucket MinIO giữ `public-read` | `MinioFileStorageService`, `docs/adr/0001-minio-public-read.md` |
| D31 | **Bug phát hiện lúc code FR-RCP-008** — thêm child entity vào aggregate đã track (Recipe load qua repository) phải gọi `IRepository<TChild>.AddAsync()` tường minh, không được chỉ dựa vào navigation fixup, nếu không EF Core đoán nhầm INSERT thành UPDATE → 409 giả | Mọi handler thêm ảnh/step/ingredient — **C đọc D31 trước khi code FR-RCP-009/010** |

## 3. File theo từng tầng (đã tạo)

### Domain — `backend/CulinaryBlog.Domain/`
- `Entities/RecipeImage.cs` — entity con, không public setter. `Create()` factory, `SetPrimary()` internal (chỉ `Recipe` gọi), `UpdateMetadata()` internal.
- `Entities/Recipe.cs` — thêm method `AttachImage()`, `SetPrimaryImage()`, `RemoveImage()` — logic "chỉ 1 ảnh primary" và "xóa primary → ảnh `orderIndex` nhỏ nhất lên thay" nằm ở đây (business rule trong Domain, không trong handler).
- `Interfaces/IRecipeRepository.cs` — thêm `GetByIdWithImagesAsync(Guid, CancellationToken)`.

### Application — `backend/CulinaryBlog.Application/`
- `Common/Files/ImageSignature.cs` — kiểm magic bytes 4 định dạng (D28), trả về `(bool IsValid, string Extension)`.
- `Common/Files/ObjectKey.cs` — sinh object key MinIO `{folder}/{Guid}{ext}` (chống path traversal, FR-FILE-001).
- `Common/Interfaces/IFileStorageService.cs`, `IBackgroundJobService.cs` — abstraction cho FR-FILE-001/002.
- `Common/Exceptions/ErrorCodes.cs` — thêm `RecipeImageNotFound` (404), `RecipePrimaryImageRequired` (400).
- `Recipes/Commands/Images/Upload/` — `UploadRecipeImageCommand`, `Handler`, `Validator` (size trước khi đọc stream → MIME → magic bytes, đúng thứ tự CONS-007/NFR-SEC-004), `UploadRecipeImageResult`.
- `Recipes/Commands/Images/Update/` — `UpdateRecipeImageCommand`, `Handler`, `Validator` (D27: rỗng → 400, `isPrimary:false` trên ảnh primary → `RECIPE_PRIMARY_IMAGE_REQUIRED`).
- `Recipes/Commands/Images/Delete/` — `DeleteRecipeImageCommand`, `Handler` (enqueue Hangfire xóa MinIO, D1: không đụng Recipe).

Mọi handler: check ownership (`recipe.AuthorId != currentUser.UserId && !currentUser.IsAdmin` → `ForbiddenException`) trước khi thao tác — đúng NFR-SEC-006 (check ở Application, không chỉ endpoint). Cache invalidation set `request.TagsToInvalidate = ["recipes", $"recipe:{slug}"]` (D8).

### Infrastructure — `backend/CulinaryBlog.Infrastructure/`
- `Files/MinioFileStorageService.cs` — `UploadAsync`/`DeleteAsync` qua AWSSDK.S3 client trỏ MinIO, bucket policy `public-read` (D16), `DeleteAsync` idempotent (không throw nếu object không tồn tại, FR-FILE-002).
- `Jobs/HangfireBackgroundJobService.cs` — thêm method enqueue xóa file (fire-and-forget, retry 3 lần theo cấu hình Hangfire mặc định).
- `Persistence/Configurations/RecipeImageConfiguration.cs` — **partial unique index** `(RecipeId) WHERE "IsPrimary"` (D27 — đảm bảo tầng DB, không chỉ Domain).
- `Persistence/Repositories/RecipeRepository.cs`, `Repository.cs` — implement `GetByIdWithImagesAsync` (có `.Include(r => r.Images)`, `.AsNoTracking()` không dùng ở đây vì cần track để mutate).
- Migration `20260922053939_AddRecipeImagePrimaryIndex`.

### API — `backend/CulinaryBlog.API/`
- `Endpoints/ImageEndpoints.cs` — 3 endpoint dưới `/recipes/{id:guid}/images`, `RequireAuthorization(Policies.Author)`, `.DisableAntiforgery()` cho multipart. Endpoint chỉ nhận request → `mediator.Send()` → `TypedResults` (CONS-008, không validate ở đây).
- `Middleware/GlobalExceptionMiddleware.cs` — map `RecipeImageNotFound`/`RecipePrimaryImageRequired` vào đúng HTTP status.

### Tests — `backend/tests/`
- **Unit:** `Domain/RecipeImageTests.cs` (logic primary/orderIndex ở Domain), `Images/ImageSignatureTests.cs` (4 định dạng + reject sai signature), `Images/ObjectKeyTests.cs`, `Images/UploadRecipeImageCommandValidatorTests.cs`, `Images/UpdateRecipeImageCommandValidatorTests.cs`.
- **Integration:** `Recipes/ImagesTests.cs` — 14 case (happy path + ownership 403 + Admin bypass + 401 + 404 recipe + 404 image + size 400 + magic-bytes 400 + set-primary swap + primary-required 400 + patch rỗng 400 + delete-primary-reassign + soft-delete-no-cascade + forbidden-delete). Hạ tầng: `Support/RecipeImagesApiFactory.cs`, `Support/FakeFileStorageService.cs` (test không cần MinIO thật).

**Định nghĩa "xong" (mục 7 CLAUDE.md):** code chạy + 14 integration test + 5 unit test suite pass + `traceability.md` đã cập nhật + 0 compiler warning.

## 4. Application Error Code liên quan (đã đăng ký trong `decisions.md`)

| Code | HTTP | Khi nào |
|---|---|---|
| `RECIPE_NOT_FOUND` | 404 | Recipe không tồn tại |
| `RECIPE_FORBIDDEN` | 403 | Không phải chủ sở hữu, không phải Admin |
| `RECIPE_IMAGE_NOT_FOUND` | 404 | PATCH/DELETE ảnh không tồn tại (D27) |
| `RECIPE_PRIMARY_IMAGE_REQUIRED` | 400 | Bỏ tick primary trên ảnh đang primary (D22) |
| `FILE_SIZE_EXCEEDED` | 400 | > 5MB |
| `FILE_MIME_INVALID` | 400 | Magic bytes không khớp 1 trong 4 định dạng (D28) |
| `VALIDATION_ERROR` | 400 | PATCH không có field nào, hoặc lỗi FluentValidation khác |

## 5. Bug/quyết định phát sinh ngoài dự kiến ban đầu

- **D27, D28** — phát hiện lúc lập kế hoạch (SRS thiếu/mâu thuẫn về response shape và signature WebP/AVIF).
- **D31** — phát hiện lúc chạy integration test thật (409 giả do EF Core đoán sai Added/Modified). Đã cảnh báo trước cho C trong `decisions.md` vì FR-RCP-009/010 dùng chung pattern aggregate-đã-track.

## 6. Frontend (`frontend/components/upload/**`, `frontend/lib/**`) — ✅ Xong

TDD đầy đủ (test trước → xác nhận fail → code → pass) cho từng file, xem `frontend/__tests__/`
tương ứng. Đã dựng thêm hạ tầng Jest + Testing Library cho frontend (chưa từng có trước đó):
`jest.config.js`, `jest.setup.ts` (gồm polyfill `HTMLDialogElement.showModal/close` — jsdom 20
chưa hiện thực, cần cho mọi component dùng `components/ui/Dialog.tsx`, không riêng feature này).

| File | Nội dung | Test |
|---|---|---|
| `lib/types.ts` | Điền `RecipeImageDto`: `{ imageId, originalUrl, mediumUrl, thumbnailUrl, altText, isPrimary, orderIndex }` | — (type only) |
| `lib/validation/imageFile.ts` | Pre-check client CONS-007 (size ≤5MB, 4 MIME) dùng chung giữa Dropzone và message lỗi của `useRecipeImages` | `__tests__/lib/validation/imageFile.test.ts` (7 case) |
| `lib/hooks/useRecipeImages.ts` | 3 mutation (upload/update/delete) qua TanStack Query, `onUploadProgress`, map lỗi theo error code (mục 4), toast NFR-USE-004 | `__tests__/lib/hooks/useRecipeImages.test.tsx` (9 case) |
| `lib/hooks/useRecipeImageGallery.ts` | Bọc `useRecipeImages`, giữ state mảng ảnh + `uploadingFiles` cục bộ (không phụ thuộc GET recipe detail — `FR-RCP-002` chưa xong). Lặp lại D22 ở client: set-primary loại trừ lẫn nhau, xoá primary → ảnh `orderIndex` nhỏ nhất lên thay | `__tests__/lib/hooks/useRecipeImageGallery.test.tsx` (7 case) |
| `components/upload/ImageUploadDropzone.tsx` | Dropzone kéo-thả + input file, pre-check client — chỉ phản hồi nhanh, server (D28 magic bytes) vẫn là nguồn thật | `__tests__/components/upload/ImageUploadDropzone.test.tsx` (6 case) |
| `components/upload/RecipeImageCard.tsx` | 1 ảnh: badge primary, hover toolbar (đặt primary/sửa alt/xoá), sửa alt-text inline (Enter lưu, Escape huỷ), fallback `thumbnailUrl → mediumUrl → originalUrl` | `__tests__/components/upload/RecipeImageCard.test.tsx` (7 case) |
| `components/upload/ImageDeleteDialog.tsx` | Tái dùng `components/ui/Dialog.tsx` + `Button` (`danger`, `loading`) | `__tests__/components/upload/ImageDeleteDialog.test.tsx` (5 case) |
| `components/upload/RecipeImageManager.tsx` | Container ghép cả 3 + `useRecipeImageGallery`, props `recipeId` + `initialImages?` | `__tests__/components/upload/RecipeImageManager.test.tsx` (10 case) |

**Tổng 53 test, 7 suite — tất cả pass.** `tsc --noEmit`, `eslint .`, `next build` đều sạch.

**Không tạo** trang `dashboard/recipes/[id]/edit` — file đó thuộc C, D chỉ cung cấp
`<RecipeImageManager recipeId={...} initialImages={...} />` để C nhúng vào bước 4 của wizard.

**Ghi chú kỹ thuật đã áp dụng khi code FE:**
- `mediumUrl`/`thumbnailUrl` = `null` ngay sau upload (FR-JOB-002 resize chạy nền, không SignalR) → `RecipeImageCard` fallback về `originalUrl`.
- Xoá ảnh yêu cầu xác nhận qua dialog (không xoá ngay khi bấm icon); đặt primary thì xử lý ngay, không cần confirm.
- `apiClient` mặc định set `Content-Type: application/json`, nhưng khi body là `FormData` axios tự bỏ header đó để trình duyệt gắn boundary — không tự set tay trong `useRecipeImages`, set tay sẽ làm hỏng multipart.
- Toast xác nhận sau mỗi write (NFR-USE-004), dùng `components/ui/Toast.tsx` có sẵn.

**Kéo-thả sắp xếp lại `orderIndex` (D27) — ✅ Xong.** `useRecipeImageGallery.reorder(draggedId, targetId)`:
di chuyển ảnh tới vị trí ảnh đích (thuật toán `arrayMove` chuẩn — xoá khỏi vị trí cũ, chèn vào
vị trí mới theo index gốc trước khi xoá), state cập nhật ngay (optimistic) rồi mới PATCH
`orderIndex` cho đúng những ảnh có index thật sự đổi (không PATCH ảnh không đổi vị trí, không
renumber toàn bộ — D27 cho phép `orderIndex` trùng). `RecipeImageCard` nhận thêm `onDragStart`/
`onDrop` (tắt draggable khi đang sửa alt-text để không xung đột chọn text), `RecipeImageManager`
giữ id ảnh đang kéo qua `useRef` và gọi `gallery.reorder` khi thả. 3 test mới ở
`useRecipeImageGallery.test.tsx`, 4 test ở `RecipeImageCard.test.tsx`, 1 test ở
`RecipeImageManager.test.tsx` — tổng 61 test / 7 suite, tất cả pass. `tsc`/`eslint`/`next build` sạch.

**Còn lại (ngoài phạm vi FR-RCP-008 FE, không làm ở đây):** nhúng `RecipeImageManager` vào wizard
thật của C — file `dashboard/recipes/[id]/edit` thuộc sở hữu C theo `team-assignment.md`.

