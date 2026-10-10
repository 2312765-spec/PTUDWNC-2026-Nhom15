# Plan — FR-JOB-002: Image Resize / Thumbnail Job

> Kế hoạch viết **trước khi code** (bước Plan, CLAUDE.md mục 9), được duyệt 2026-10-07.
> Người phụ trách: D (Media & Vận hành) — 2312765@dlu.edu.vn.
> Branch: `feature/FR-JOB-002-image-resize`.

---

## 1. Phạm vi

| FR | Nội dung | Trạng thái |
|---|---|---|
| FR-JOB-002 | Sau upload ảnh (FR-RCP-008), Hangfire fire-and-forget sinh thumbnail 300×300 + medium 800×600, lưu MinIO, cập nhật `MediumUrl`/`ThumbnailUrl`. Retry 3 lần; fail thì ảnh gốc vẫn hiển thị | ⬜ Chưa làm |

**Ngoài phạm vi:** đưa `mediumUrl`/`thumbnailUrl` ra API (thuộc FR-RCP-002 — chưa làm; FE đã
khai báo sẵn trong `lib/types.ts` và fallback về `originalUrl`). Không cần migration — cột
`MediumUrl`/`ThumbnailUrl` đã có (SRS 7.5). ImageSharp 3.1.12 đã có trong `Infrastructure.csproj`.

## 2. Quyết định D-x áp dụng

| D-x | Ảnh hưởng |
|---|---|
| D27 | Response 201 của upload không chờ job — chỉ có `originalUrl` |
| D28 | Job phải xử lý được cả 4 định dạng JPEG/PNG/WebP/AVIF (xem D41) |
| D8 | Cập nhật URL ảnh = thao tác Image → xóa tag `recipes` + `recipe:{slug}` |
| D16 | Bucket public-read → file phái sinh cũng public, tên file phải có GUID |
| D1 / D22 | Recipe bị soft delete hoặc ảnh bị `DELETE` trước khi job chạy → job no-op |
| D4 / D34 | Job ghi `RecipeImage` có thể đụng RowVersion với `PATCH` đồng thời → để Hangfire retry |

## 3. Quyết định mới — thêm vào `docs/decisions.md` TRƯỚC khi code

| D | Chủ đề | Chốt |
|---|---|---|
| **D40** | Cách resize | Thumbnail: crop giữa ảnh, đúng 300×300. Medium: thu cho lọt 800×600, giữ tỉ lệ, không crop. **Không upscale** ảnh nhỏ hơn đích. Auto-orient theo EXIF rồi bỏ metadata EXIF (tránh lộ GPS) |
| **D41** | Định dạng & AVIF | Ảnh phái sinh luôn **WebP** (quality 80). Ảnh gốc **AVIF → job no-op**, log Information, 2 URL giữ null — ImageSharp 3.1 không decode được AVIF |
| **D42** | Retry | 3 lần, chờ **1 / 5 / 30 phút** (giống FR-JOB-001). Hết lượt → Failed + log Error |
| **D43** | Xóa ảnh | `DELETE` ảnh enqueue xóa cả original + medium + thumbnail (bỏ qua URL null) |
| **D44** | Luồng job | Job chỉ gọi MediatR command `GenerateRecipeImageVariantsCommand` (CONS-002, D8). File phái sinh lưu `recipes/{recipeId}/{guid}.webp` qua `ObjectKey`. Job idempotent |
| **D45** | Bật Hangfire server | Bỏ comment `AddHangfireServer()` trong `Program.cs` (giữ điều kiện `!IsEnvironment("Testing")`). Xem lý do bên dưới |

**Lý do D45 (tra từ git log):** server bị comment trong `4b78dfb` (2026-09-26) — commit đó
đồng thời xóa `AddHangfire(...)` và thay service thật bằng Mock, nên server không còn storage
để chạy. `1d5fad9` (2026-09-28) khôi phục `AddHangfire` nhưng không bật lại server, coi nhầm
trạng thái tạm đó là thiết kế. Hệ quả: job được enqueue vào Postgres nhưng **không bao giờ
chạy** — ảnh hưởng cả FR-JOB-001 (welcome email) và FR-FILE-002 (xóa file). Lo ngại "host không
lên khi Postgres chưa sẵn sàng" không đáng kể: SRS mục 2.x ghi Postgres down là "toàn bộ hệ
thống ngừng", và NFR-REL-002 nói về Redis chứ không phải Postgres. `SmokeTests` vẫn an toàn vì
môi trường `Testing` không bật server.

⚠️ `Program.cs` là file dùng chung — **báo A (FR-JOB-001) và Yen trước khi merge**: bật server
thì welcome email bắt đầu gửi thật.

Sửa SRS (qua CR): FR-JOB-002 bổ sung cách resize / định dạng / lịch retry; FR-FILE-002 bổ sung
xóa cả ảnh phái sinh.

## 4. Luồng

```
POST /recipes/{id}/images  (UploadRecipeImageCommandHandler)
  └─ transaction thành công → backgroundJobService.EnqueueGenerateImageVariants(imageId)
       (enqueue lỗi → log warning, KHÔNG fail request — ảnh gốc vẫn dùng được)

Hangfire → ResizeRecipeImageJob.ExecuteAsync(imageId)   [AutomaticRetry 3 lần, 60/300/1800s]
  └─ sender.Send(GenerateRecipeImageVariantsCommand(imageId))
       1. Load Recipe + Images; không thấy ảnh / recipe đã soft delete → no-op
       2. Ảnh đã có ThumbnailUrl → no-op (idempotent)
       3. Ảnh gốc AVIF → no-op + log (D41)
       4. fileStorage.DownloadAsync(originalUrl)
       5. imageResizer.Resize → fileStorage.UploadAsync × 2
       6. recipe.SetImageVariants(imageId, medium, thumb) → SaveChanges
          (lỗi ở đây → enqueue xóa 2 file vừa upload, rethrow để Hangfire retry)
       7. TagsToInvalidate = ["recipes", "recipe:{slug}"]
```

## 5. File tạo / sửa

### Domain
| File | Thay đổi |
|---|---|
| `Entities/Recipe.cs` | `SetImageVariants(Guid imageId, string mediumUrl, string thumbnailUrl)` — `DomainException` nếu ảnh không tồn tại / URL rỗng |
| `Entities/RecipeImage.cs` | `internal void SetVariants(...)` |

### Application
| File | Thay đổi |
|---|---|
| `Common/Interfaces/IImageResizer.cs` (**mới**) | `ResizedImages? Resize(Stream original)` — `null` nếu định dạng không hỗ trợ |
| `Common/Interfaces/IFileStorageService.cs` | Thêm `Task<Stream> DownloadAsync(string fileUrl, CancellationToken)` |
| `Common/Interfaces/IBackgroundJobService.cs` | Thêm `EnqueueGenerateImageVariants(Guid imageId)` |
| `Recipes/Commands/Images/GenerateVariants/GenerateRecipeImageVariantsCommand.cs` (**mới**) | `record(Guid ImageId) : IRequest, ICacheInvalidator` |
| `.../GenerateRecipeImageVariantsCommandHandler.cs` (**mới**) | Bước 1–7 mục 4. Không check ownership (lệnh hệ thống, không có endpoint) |
| `.../GenerateRecipeImageVariantsCommandValidator.cs` (**mới**) | `ImageId` khác rỗng |
| `Recipes/Commands/Images/Upload/UploadRecipeImageCommandHandler.cs` | Enqueue sau transaction, try/catch + log |
| `Recipes/Commands/Images/Delete/DeleteRecipeImageCommandHandler.cs` | Enqueue xóa cả 3 URL (D43) |

### Infrastructure
| File | Thay đổi |
|---|---|
| `Files/ImageSharpImageResizer.cs` (**mới**) | Hiện thực D40/D41. Giới hạn kích thước decode (vd. 40 megapixel) chống decompression bomb |
| `Files/MinioFileStorageService.cs` | `DownloadAsync` qua `GetObjectAsync` → `MemoryStream` (≤ 5 MB) |
| `Jobs/ResizeRecipeImageJob.cs` (**mới**) | `[AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 1800], OnAttemptsExceeded = Fail)]`, gọi `ISender.Send` |
| `Jobs/HangfireBackgroundJobService.cs` | Hiện thực `EnqueueGenerateImageVariants` |
| `DependencyInjection.cs` ⚠️ dùng chung | Đăng ký `IImageResizer`, `ResizeRecipeImageJob` |

### API
| File | Thay đổi |
|---|---|
| `Program.cs` ⚠️ dùng chung | Bỏ comment `AddHangfireServer()` (D45) |

## 6. Test (viết trước, xác nhận fail)

| Loại | File | Nội dung |
|---|---|---|
| Integration | `IntegrationTests/Jobs/ImageResizeTests.cs` | `RecipeImagesApiFactory`: upload PNG thật qua API → gửi command → DB có 2 URL + 2 file WebP. Case: ảnh đã xóa → no-op; chạy 2 lần → no-op; AVIF → no-op |
| Integration | `Files/MinioFileStorageServiceTests.cs` | `DownloadAsync` với S3Mock |
| Integration | `Recipes/ImagesTests.cs` | Upload → job được enqueue; xóa ảnh → xóa cả 3 URL |
| Unit | `UnitTests/Images/GenerateRecipeImageVariantsCommandHandlerTests.cs` | Các nhánh no-op, happy path, SaveChanges lỗi → enqueue xóa 2 file vừa upload |
| Unit | `UnitTests/Files/ImageSharpImageResizerTests.cs` | Thumbnail đúng 300×300; medium giữ tỉ lệ ≤ 800×600; không upscale; đầu ra là WebP (magic bytes); AVIF → null |
| Unit | `UnitTests/Domain/RecipeImageTests.cs` | `SetImageVariants` |
| Unit | `UnitTests/Jobs/ResizeRecipeImageJobTests.cs` | `AutomaticRetry` = 3 lần, 60/300/1800s |
| Unit | `UploadRecipeImageCommandHandlerTests.cs` | Enqueue đúng `imageId`; enqueue lỗi → request vẫn thành công |

`FakeFileStorageService` cần lưu nội dung để hiện thực `DownloadAsync`. Integration test gọi
command trực tiếp vì môi trường `Testing` không chạy Hangfire server.

## 7. Thứ tự commit

1. `docs(FR-JOB-002): thêm D40–D45`
2. `test(FR-JOB-002): ...` — chạy, xác nhận fail
3. `feat(FR-JOB-002): ...` — Domain → Application → Infrastructure
4. `chore(FR-JOB-002): bật Hangfire server (D45)` — sau khi báo nhóm
5. `docs: cập nhật traceability FR-JOB-002` — bỏ ghi chú "cố ý để lại"
