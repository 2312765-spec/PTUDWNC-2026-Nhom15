# Plan — FR-JOB-001: Welcome Email Job

> Kế hoạch viết **trước khi code** (bước Plan, CLAUDE.md mục 9), được duyệt 2026-10-21.
> Người phụ trách: A (Auth) — 2312800@dlu.edu.vn.
> Branch: `2312800_VCVinh_FR-JOB-001`.

---

## 1. Phạm vi

| FR | Nội dung | Trạng thái |
|---|---|---|
| FR-JOB-001 | Hangfire fire-and-forget sau đăng ký. Email **HTML** gồm tên người dùng, link trang chủ và link `/dashboard` (D12). Retry 3 lần, chờ 1 / 5 / 30 phút. Hết lượt → Failed + log Error | ⬜ — đã có bản tối giản từ FR-AUTH-001 (plain text, retry mặc định 10 lần của Hangfire) |

**Ngoài phạm vi:** link kích hoạt / xác nhận email (D12), SendGrid, template đa ngôn ngữ, gửi lại email thủ công.

## 2. Quyết định D-x áp dụng

| D-x | Ảnh hưởng |
|---|---|
| D12 | Không có link kích hoạt; chỉ link `/` và `/dashboard` |
| D42 | `[AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 1800], OnAttemptsExceeded = Fail)]` |
| D44 | Mẫu job: Hangfire job chỉ `sender.Send(command)`, logic ở Application |
| D45 | Hangfire server đã bật (trừ `Testing`) |
| D5 | Dùng `displayName` |

## 3. Quyết định mới — thêm vào `docs/decisions.md` TRƯỚC khi code

| D | Chủ đề | Chốt |
|---|---|---|
| **D55** | Enqueue welcome email lỗi | **Log Warning, không làm fail request.** User đã tạo, token đã cấp → vẫn trả 201/200. Vì SRS mục 307: Hangfire hỏng "không ảnh hưởng core functionality"; giống D44 cho ảnh. Áp dụng cả Register lẫn Google login |
| **D56** | Ai nhận welcome email | Tài khoản tạo qua **FR-AUTH-001** và **tài khoản Google mới** (FR-AUTH-003, `isNewUser = true`). Google liên kết vào tài khoản có sẵn → **không** gửi. Vì cả hai đều là tạo tài khoản mới; roadmap S9 gộp chung hai FR |
| **D57** | Nội dung & cấu hình email | Link dựng từ `Seo:SiteBaseUrl` (dùng lại key của FR-JOB-003). Base URL đi qua command, kiểm bằng validator: thiếu/sai → `ValidationException` → retry → Failed (giống D52). `displayName` **luôn HTML-encode**. Email multipart: HTML + plain text dự phòng. SMTP: có `Smtp:Username` thì `AuthenticateAsync`; TLS `SecureSocketOptions.Auto` (Mailhog không TLS, production STARTTLS) |

## 4. Luồng

```
RegisterCommandHandler / GoogleLoginCommandHandler (isNewUser)
  → IBackgroundJobService.EnqueueWelcomeEmail(email, displayName)   [try/catch → LogWarning, D55]
    → Hangfire: WelcomeEmailJob.ExecuteAsync(email, displayName)
          [AutomaticRetry(3, 60/300/1800, Fail)] — D42
      → ISender.Send(SendWelcomeEmailCommand(email, displayName, Seo:SiteBaseUrl))
          LoggingBehavior    → lỗi thì LogError (mỗi lần thử, kể cả lần cuối)
          ValidationBehavior → SendWelcomeEmailCommandValidator
        → SendWelcomeEmailCommandHandler
            WelcomeEmailTemplate.Build(displayName, baseUrl) → (subject, html, text)
            IEmailService.SendAsync(EmailMessage)
              → MailKitEmailService (SMTP / Mailhog)
```

"Log Error khi hết lượt" có sẵn nhờ `LoggingBehavior`: lần thử cuối lỗi → log Error, Hangfire chuyển
job sang Failed (xem ở `/hangfire`). Không cần filter Hangfire riêng.

## 5. File theo tầng

### Domain
- Không đổi. Email không phải entity, không có business rule ở Domain.

### Application
- **Sửa** `Common/Interfaces/IEmailService.cs` — `SendWelcomeEmailAsync(...)` → `SendAsync(EmailMessage message, ct)`.
  Infrastructure chỉ lo gửi, nội dung ở Application để unit test được. **Hợp đồng chung — chủ sở hữu A.**
  Chỉ có `MailKitEmailService` implement.
- **Tạo** `Common/Email/EmailMessage.cs` — record `(ToAddress, ToName, Subject, HtmlBody, TextBody)`.
- **Tạo** `Auth/Commands/SendWelcomeEmail/SendWelcomeEmailCommand.cs` — `(Email, DisplayName, SiteBaseUrl) : IRequest`.
  Không implement `ICacheInvalidator`.
- **Tạo** `Auth/Commands/SendWelcomeEmail/SendWelcomeEmailCommandHandler.cs`.
- **Tạo** `Auth/Commands/SendWelcomeEmail/SendWelcomeEmailCommandValidator.cs` — email đúng format,
  `DisplayName` không rỗng, `SiteBaseUrl` URL tuyệt đối http(s).
- **Tạo** `Auth/Email/WelcomeEmailTemplate.cs` — hàm thuần, `WebUtility.HtmlEncode(displayName)`,
  inline CSS đơn giản, link `{base}/` và `{base}/dashboard`.
- **Sửa** `Auth/Commands/Register/RegisterCommandHandler.cs`, `Auth/Commands/GoogleLogin/GoogleLoginCommandHandler.cs` —
  bọc enqueue trong try/catch → log Warning (D55); inject `ILogger<>`.
- **Sửa** comment `Common/Interfaces/IBackgroundJobService.cs`.

### Infrastructure
- **Tạo** `Jobs/WelcomeEmailJob.cs` — `[AutomaticRetry(...)]` (D42), `ExecuteAsync(email, displayName, ct)`
  đọc `Seo:SiteBaseUrl` → `sender.Send`, giống `SitemapGenerationJob`.
- **Sửa** `Jobs/HangfireBackgroundJobService.cs` — enqueue `WelcomeEmailJob` thay vì `IEmailService`.
- **Sửa** `Email/MailKitEmailService.cs` — `SendAsync(EmailMessage)` multipart/alternative, `AuthenticateAsync`
  khi có `Smtp:Username`. Bỏ `LogWarning` (`LoggingBehavior` đã log Error), lỗi ném lại cho Hangfire retry.
- **Sửa** `DependencyInjection.cs` — đăng ký `WelcomeEmailJob`.

### API
- **Sửa** `appsettings.json` — thêm `"Smtp": { "Username": "", "Password": "" }` (rỗng; secret thật qua
  User Secrets / env vars). Không sửa `Program.cs`.

### Frontend

Email trỏ tới `{SiteBaseUrl}/dashboard` (D12) nhưng `app/dashboard/` chưa có `page.tsx` → người dùng bấm link
trong email sẽ gặp **404**. Bổ sung trang đích tối giản (bổ sung 2026-10-21, sau khi phát hiện lúc rà frontend):

| File | Việc |
|---|---|
| `app/dashboard/page.tsx` (tạo) | Client Component (SRS 5.1: `/dashboard/*` CSR). Đọc `useSession()` — **không** gọi thêm API, giống `HeaderAuth`. Đang tải phiên → skeleton (NFR-USE-004). Chưa đăng nhập → `router.replace('/auth/login?callbackUrl=%2Fdashboard')` (cùng mẫu `dashboard/categories/layout.tsx`). Đã đăng nhập → tiêu đề "Chào mừng, {displayName}!" + lối tắt: **Hồ sơ của tôi** (`/dashboard/profile`), **Khám phá công thức** (`/recipes`), và **Quản lý danh mục** (`/dashboard/categories`) chỉ khi có role `Admin`. |
| `__tests__/app/dashboard/page.test.tsx` (tạo) | Viết **trước**, xác nhận fail rồi mới tạo trang. |

- Không tạo `app/dashboard/layout.tsx`: layout ở cấp này sẽ bao luôn `/dashboard/categories` (B) và `/dashboard/recipes/**` (C).
- `displayName` hiển thị qua JSX nên React tự escape — không dùng `dangerouslySetInnerHTML`.
- Trang chỉ là **khung chào mừng**. "Tổng quan Author" (số recipe, trạng thái bài…) thuộc FR-RCP của C
  (`app/dashboard/OWNER.md`, `team-assignment.md`) — C mở rộng file này, không viết lại từ đầu.

## 6. Test — viết trước, xác nhận fail

**`IntegrationTests/Jobs/WelcomeEmailTests.cs`** (`RecipeImagesApiFactory` — Postgres + `RecordingBackgroundJobService`):
1. `POST /auth/register` thành công → enqueue welcome email đúng email + displayName
   (thêm `WelcomeEmails` vào `RecordingBackgroundJobService`).
2. Google login tài khoản mới → enqueue; Google liên kết tài khoản có sẵn → **không** enqueue (D56).
3. *(error)* Đăng ký email trùng (409) → **không** enqueue.
4. *(D55)* Enqueue ném exception → register vẫn **201** với đủ token.
5. `WelcomeEmailJob` có `AutomaticRetry` Attempts = 3, Delays = [60, 300, 1800], `Fail` (reflection, như `SitemapTests`).
6. Chạy job (fake `IEmailService` ghi lại) → đúng người nhận, HTML có displayName đã encode, link `/` và
   `/dashboard` theo `Seo:SiteBaseUrl`, **không** có "kích hoạt"/"verify" (D12).
7. *(error)* Thiếu `Seo:SiteBaseUrl` → `ValidationException`, không gửi.

**`IntegrationTests/Email/MailKitEmailServiceTests.cs`** (SMTP thật — Testcontainers `mailhog/mailhog`, đọc
thư qua HTTP API cổng 8025):
1. Gửi multipart đúng From / To / Subject, có cả HTML và text.
2. *(error)* SMTP không kết nối được → ném exception (Hangfire retry), không nuốt lỗi.

**`UnitTests/Auth/`**:
- `SendWelcomeEmailCommandHandlerTests.cs` — gọi `IEmailService` đúng 1 lần với message từ template.
- `SendWelcomeEmailCommandValidatorTests.cs` — email sai, displayName rỗng, base URL thiếu / tương đối / `ftp://`.
- `WelcomeEmailTemplateTests.cs` — `<script>` bị encode, base URL có `/` cuối không sinh `//dashboard`, có bản text.
- **Sửa** `RegisterCommandHandlerTests.cs`, `GoogleLoginCommandHandlerTests.cs` — enqueue ném lỗi → handler vẫn trả kết quả (D55).

**`frontend/__tests__/app/dashboard/page.test.tsx`** (Jest + Testing Library, mock `next-auth/react` và `next/navigation`):
1. Đang tải phiên → skeleton `aria-label="Đang kiểm tra phiên đăng nhập"`, chưa có lời chào.
2. *(error)* Chưa đăng nhập → `router.replace('/auth/login?callbackUrl=%2Fdashboard')`, không hiện nội dung.
3. Đã đăng nhập → heading "Chào mừng, {displayName}!" + link Hồ sơ (`/dashboard/profile`) và Khám phá công thức (`/recipes`).
4. Author → **không** có link Quản lý danh mục; Admin → có link `/dashboard/categories`.
5. `displayName` chứa `<script>` → hiện nguyên văn như text, không tạo phần tử `script`.

## 7. Rủi ro & lưu ý

- `app/dashboard/page.tsx` thuộc phạm vi C (`OWNER.md`). Bản của FR-JOB-001 chỉ là khung tối thiểu để link email
  không 404 — **báo C** trước khi merge để C không tạo trùng file / xung đột merge.

- Image Mailhog có thể bị chặn pull như MinIO → chuyển `rnwood/smtp4dev` (cũng có HTTP API), ghi vào mục "Lệch".
- Job cũ trong queue có kiểu `IEmailService.SendWelcomeEmailAsync` sẽ lỗi deserialize sau khi đổi interface.
  Dev: xóa schema `hangfire`. Production chưa có dữ liệu → chấp nhận.
- `RecordingBackgroundJobService` là file test dùng chung — báo C và D khi thêm thuộc tính.

## 8. Xong khi

- Toàn bộ test trên pass, `dotnet build` 0 warning.
- D55–D57 đã thêm vào `decisions.md`.
- `traceability.md`: FR-JOB-001 ✅ kèm file + test; sửa bảng tổng kết FR-AUTH đang ghi lệch "4 / 0 / 3".
- `CHANGELOG.md` cập nhật.
- Kiểm tra tay: đăng ký trên local → thấy email HTML ở Mailhog `http://localhost:8025`.
