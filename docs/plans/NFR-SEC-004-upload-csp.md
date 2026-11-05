# Plan — NFR-SEC-004: Input Validation & File Upload Security

> Kế hoạch viết **trước khi code** (bước Plan, CLAUDE.md mục 9), được duyệt 2026-11-05.
> Người phụ trách: D (Media & Vận hành) — 2312765@dlu.edu.vn.
> Branch: `feature/NFR-SEC-004-upload-csp` (tách từ `chore/CONS-009-prod-s3` — nối số D sau D66).

---

## 1. Yêu cầu (SRS §NFR-SEC-004)

Validate tại Application Layer (FluentValidation) **trước** khi xử lý. SQL injection: EF Core
parameterized, không raw SQL với input. XSS: sanitization + **Content-Security-Policy header**. MIME:
magic bytes, không tin `Content-Type`. File size: kiểm tra **trước khi đọc stream**. Path traversal:
tên file GUID.

## 2. Đối chiếu hiện trạng (2026-11-05)

| Yêu cầu | Hiện trạng | Đánh giá |
|---|---|---|
| FluentValidation trước handler | `ValidationBehavior` trong pipeline MediatR | ✅ |
| Magic bytes | `ImageSignature` + `UploadRecipeImageCommandValidator` (D28); unit + integration, gồm `.exe` → `.jpg` | ✅ |
| Size trước khi đọc stream | Validator kiểm `Length` trước (unit test riêng). **Nhưng** endpoint nhận `IFormFile` → ASP.NET đã nhận + đệm toàn bộ body (tới 30MB, mặc định Kestrel) trước validator. Nginx chặn 6M nhưng chỉ đứng trước API | ⚠️ Lỗ hổng 1 |
| Tên file GUID | `ObjectKey` + unit test `../`; chưa có integration test qua endpoint | 🟡 |
| Không raw SQL | Một `ExecuteSqlInterpolatedAsync` (tham số hóa — CONS-006 cho phép); không có test chặn `FromSqlRaw` | 🟡 |
| XSS + CSP | React escape; JSON-LD escape có test. **Không có `Content-Security-Policy`** ở Nginx, Next.js, API | ❌ Lỗ hổng 2 |
| (phát sinh) | Body vượt giới hạn Kestrel → `BadHttpRequestException` → `GlobalExceptionMiddleware` không map → **500** | ❌ Lỗ hổng 3 |

## 3. Quyết định mới — thêm vào `docs/decisions.md` TRƯỚC khi code

| D | Chủ đề | Chốt |
|---|---|---|
| **D67** | Giới hạn kích thước ở tầng HTTP | Endpoint upload giới hạn form **6MB** (5MB + dư multipart, khớp `client_max_body_size 6M`). Vượt → **413** RFC 7807 `FILE_SIZE_EXCEEDED` (không còn 500). 5–6MB: validator vẫn 400 `FILE_SIZE_EXCEEDED`. Là cấu hình transport, không phải validation nghiệp vụ → không vi phạm CONS-008 |
| **D68** | Chính sách CSP | Trang Next.js: `default-src 'self'`; `script-src 'self' 'unsafe-inline'` (nonce buộc render động mọi trang → phá ISR SRS 5.1 — đánh đổi ghi rõ); `style-src 'self' 'unsafe-inline'`; `img-src` site + S3 + `data:`/`blob:`; `connect-src` site + API; `object-src 'none'`; `base-uri 'self'`; `frame-ancestors 'none'`; `form-action 'self'` + Google OAuth. Dev thêm `'unsafe-eval'` (hot reload). API: `default-src 'none'; frame-ancestors 'none'`. Mỗi response đúng **một** header CSP (không thêm ở Nginx) |

## 4. File sửa

| Tầng | File | Việc |
|---|---|---|
| API | `Endpoints/ImageEndpoints.cs` | Giới hạn form 6MB trên endpoint upload (`WithFormOptions`) |
| API | `Middleware/GlobalExceptionMiddleware.cs` | Map `BadHttpRequestException` → status của nó (413 → `FILE_SIZE_EXCEEDED`) |
| API | Middleware header bảo mật (mới) | CSP cho mọi response API |
| Frontend | `lib/security/csp.ts` + `next.config.ts` | Header CSP mọi trang, biến thể dev/prod |
| Ops | `scripts/verify-prod.sh` | Kiểm header CSP trên trang + API |
| Docs | `decisions.md` (D67, D68), `traceability.md` (NFR-SEC-004 → ✅), CHANGELOG | |

## 5. Test (viết trước, xác nhận fail)

| Test | Phủ |
|---|---|
| `IntegrationTests/Security/UploadSecurityTests.cs` — body 10MB → 413 `FILE_SIZE_EXCEEDED` | Lỗ hổng 1, 3 |
| — tên file `../../../etc/passwd.jpg` → URL ảnh dạng `recipes/{id}/{guid}.jpg` | Path traversal |
| `IntegrationTests/Security/CspTests.cs` — response API có CSP `default-src 'none'` | Lỗ hổng 2 (API) |
| `IntegrationTests/Security/SqlInjectionTests.cs` — tìm kiếm `' OR 1=1 --` → 200, không 500 | SQL injection |
| `ArchitectureTests` — cấm gọi `FromSqlRaw` / `ExecuteSqlRaw` / `SqlQueryRaw` | SQL injection (chặn hồi quy) |
| Jest — builder CSP đúng chỉ thị (dev/prod) | Lỗ hổng 2 (FE) |
| Jest — tiêu đề công thức `<img src=x onerror=…>` hiển thị dạng chữ | XSS output encoding |

Sau khi thêm CSP: chạy lại Lighthouse CI (không lỗi CSP trên console, `next/image` vẫn tải) và
`verify-prod.sh`.

## 6. Lưu ý

- `next.config.ts`, `GlobalExceptionMiddleware` là file dùng chung → PR cần nhóm review.
- `'unsafe-inline'` cho script làm CSP yếu hơn nonce — đánh đổi để giữ ISR (D68).

## 7. Kết quả

### Test first (2026-11-05)

| Test | Kết quả trước khi code | Ý nghĩa |
|---|---|---|
| `ImagesTests.Upload_BodyOverFormLimit_Returns413FileSizeExceeded` | ✗ (400 — cả 10MB tới validator) | Lỗ hổng 1, 3 |
| `CspTests` (3 case: `/health/live`, 404 RFC 7807, 404 routing) | ✗ (không có header) | Lỗ hổng 2 (API) |
| `__tests__/lib/security/csp.test.ts` (8) | ✗ (chưa có module) | Lỗ hổng 2 (FE) |
| `ImagesTests.Upload_FileTooLarge_Returns400FileSizeExceeded` (đổi 6MB+1 → 5,5MB theo D67) | ✓ | Nhánh 400 của validator giữ nguyên |
| `ImagesTests.Upload_PathTraversalFileName_StoresUnderRecipeFolder` | ✓ | Chứng minh + chặn hồi quy |
| `MinioFileStorageServiceTests.UploadAsync_PathTraversalFileName_KeyIsGuidUnderFolder` (S3 thật) | ✓ | Chứng minh + chặn hồi quy |
| `SqlInjectionTests` (4 payload + DROP TABLE + slug) | ✓ | Chứng minh + chặn hồi quy |
| `ArchitectureTests/SecurityRuleTests` (cấm raw SQL + đối chứng) | ✓ | Chặn hồi quy |
| `RecipeCard.xss.test.tsx` | ✓ | React escape — chứng minh |

### Code + kiểm tra (2026-11-05)

| Kiểm tra | Kết quả |
|---|---|
| `dotnet test` toàn bộ | Unit 291/291 · Integration 306/306 · Architecture 12/12 (1 skip có sẵn) |
| Jest toàn bộ · lint · typecheck · build | 457/457 · sạch · sạch · `/` vẫn ISR 1h |
| API thật: header | `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` |
| Playwright 7 trang (`next start` + API thật) | Đủ CSP, hydrate đủ, **0 vi phạm CSP**. Lỗi console còn lại là lỗi server có sẵn của máy local: Auth.js `MissingSecret` (không có `.env.local`), ảnh seed `placehold.co` bị chặn ở chế độ production — không liên quan CSP |
| `verify-prod.sh` (stack prod build mới) | Bước 1–8 đạt, gồm 2 kiểm tra CSP mới (đúng 1 header, Nginx không thêm). Bước 9: lỗi log cũ trong volume đã biết |

**Phát hiện lúc code:**
- Form vượt giới hạn: minimal API trả **400 rỗng** (Testing/Production) hoặc ném lỗi → **500** (Development,
  vì `ThrowOnBadRequest` mặc định chỉ bật ở Development) → bật `ThrowOnBadRequest` mọi môi trường + map
  trong `GlobalExceptionMiddleware` (D67.3).
- `GlobalExceptionMiddleware` gán `ContentType = application/problem+json` rồi `WriteAsJsonAsync` ghi đè
  thành `application/json` → mọi response lỗi sai CONS-005 từ trước. Đã sửa.
- API phục vụ trang HTML Scalar (`/scalar`, Development) cần script CDN → middleware CSP bỏ qua đường dẫn này.
- Kiểm CSP lần đầu đo nhầm một `next start` cũ còn sót (build trước khi có CSP, giữ cổng 3000) → dừng
  process đó, đo lại.

**Khác plan:** (1) test upload đặt trong `ImagesTests` (dùng lại helper + factory Postgres) thay vì file
`UploadSecurityTests.cs` mới. (2) Kiểm chứng architecture test: thêm tạm `FromSqlRaw(sql)` vào
`RecipeRepository` → test đỏ đúng; hoàn tác. Lần thử đầu (`FromSqlRaw` nối chuỗi) bị analyzer **EF1003**
+ `TreatWarningsAsErrors` chặn ngay lúc build — lớp bảo vệ có sẵn; architecture test bổ sung cho trường
hợp analyzer không bắt (chuỗi hằng/biến). Test đối chứng phát hiện pattern ban đầu (`\.Tên\(`) không khớp
định dạng Mono.Cecil (`Kiểu::Tên(`) — đã sửa.
