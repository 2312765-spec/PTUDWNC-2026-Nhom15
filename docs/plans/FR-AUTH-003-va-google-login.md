# Plan — FR-AUTH-003: Vá các khoảng trống đăng nhập Google

> Người phụ trách: A (Xác thực & Tài khoản) — 2312800@dlu.edu.vn.
> Kế hoạch viết **trước** khi code (bước Plan, mục 9 `CLAUDE.md`).

---

## 1. Phạm vi

FR-AUTH-003 **đã xong** (`traceability.md` ✅, merge qua PR #21 và PR #23):
`GoogleLoginCommand(+Handler,+Validator)`, `IGoogleTokenValidator`/`GoogleTokenValidator`,
`IdentityService.LoginOrRegisterWithGoogleAsync`, `BadGatewayException` → 502,
`Auth/GoogleLoginTests.cs` (8 integration test); FE provider `google` trong `auth.ts`,
`authorizeGoogle`, `GoogleSignInButton` trên `/auth/login` + `/auth/register` (13 test Jest).

Plan này chỉ **lấp các khoảng trống** phát hiện khi đối chiếu code với SRS + `decisions.md` + NFR:

| # | Khoảng trống | Mức độ | Trạng thái |
|---|---|---|---|
| G1 | Tài khoản `IsActive = false` vẫn đăng nhập được qua Google — bypass D11 | **Cao** — bảo mật | ⬜ |
| G2 | FE gộp mọi lỗi Google (400/403/502) thành một toast chung | Trung bình | ⬜ |
| G3 | Chưa có unit test cho `GoogleLoginCommandHandler` (NFR-MAINT-002) | Trung bình | ⬜ |
| G4 | Tạo user mới (Create → AddToRole → AddLogin) không trong transaction | Thấp | ⬜ |

**Ngoài phạm vi:**

- E2E Playwright cho Google — không thuộc 5 luồng E2E bắt buộc, và Google Consent Screen
  thật không tự động hóa được. Hạ tầng Playwright nằm ở plan FR-AUTH-002 (G3).
- FR-JOB-001 (email chào mừng) — FR riêng.

## 2. Phân tích

### G1 — Tài khoản bị vô hiệu vẫn đăng nhập được qua Google

`IsActive` chỉ được kiểm tra ở hai chỗ:

- `IdentityService.ValidateCredentialsAsync` (login mật khẩu, FR-AUTH-002).
- `IdentityService.GetUserForRefreshAsync` (refresh, FR-AUTH-004).

`LoginOrRegisterWithGoogleAsync` không kiểm tra, và không có `OnTokenValidated` nào chặn ở
tầng JWT. Hệ quả: user bị vô hiệu bấm "Tiếp tục với Google" → **200 + access token dùng
được 15 phút**, chỉ bị chặn (403) ở lần refresh đầu tiên. Tệ hơn, nếu tài khoản bị vô hiệu
chưa liên kết Google thì luồng còn gọi `AddLoginAsync` — ghi thêm dữ liệu vào tài khoản đã bị
khóa.

### G2 — Lỗi Google không phân biệt được trên UI

`authorizeGoogle` chạy trong `profile()` của provider Google. Khi `profile()` ném lỗi,
Auth.js chỉ redirect về `/auth/login?error=<mã chung>` — không mang theo `type` của
ProblemDetails. `login/page.tsx` coi mọi `?error` (trừ `SessionExpired`) là lỗi Google →
một toast "Không thể xác thực với Google lúc này".

D33 giữ riêng 502 chính là để frontend phân biệt "thử lại sau" (Google sập) với "cần đăng
nhập lại" (token hỏng / email chưa xác minh). Sau G1 còn thêm 403 "tài khoản bị vô hiệu".

### G3 — Thiếu unit test handler

Các nhánh của `GoogleLoginCommandHandler` (email chưa xác minh, user mới → enqueue welcome
email, user cũ → không enqueue, lưu hash refresh token) chỉ được phủ gián tiếp qua integration
test. Handler refresh đã có `RefreshTokenCommandHandlerTests.cs` — Google nên có tương tự để
đạt ≥ 80% coverage tầng Application.

### G4 — Tạo user không atomic

`CreateAsync` → `AddToRoleAsync` → `AddLoginAsync` là 3 lần `SaveChanges` riêng. Bước 2 hoặc 3
lỗi → DB còn user không role, không Google login. Lần Google login sau tìm thấy user theo
email → liên kết → phát token với `roles = []` → user không dùng được chức năng Author.

## 3. Quyết định D-x áp dụng

| D-x | Tóm tắt | Ảnh hưởng |
|---|---|---|
| D9 | Body `{ idToken }`, backend verify với Google, liên kết tài khoản trùng email | Giữ nguyên |
| D33 | 400 `AUTH_GOOGLE_TOKEN_INVALID`; 502 `AUTH_GOOGLE_UNAVAILABLE` | G2 đưa phân biệt này lên UI |
| D11 | `IsActive == false` → 403 `AUTH_ACCOUNT_DISABLED` | G1 áp thêm cho luồng Google |
| D35-3 | Lockout chỉ chặn đoán mật khẩu | Luồng Google **không** kiểm tra lockout — giữ nguyên |
| D23 | Application không đụng `UserManager` | Thay đổi G1/G4 nằm trong `IdentityService` (Infrastructure) |
| **D37** *(cần thêm — chờ chốt)* | Xem mục 4 | Bổ sung luồng ngoại lệ SRS chưa có |

> D36 đã được đề xuất trong plan FR-AUTH-002 nên dùng số D37.

## 4. D37 — đề xuất (thêm vào `decisions.md` trước khi code)

**Nguồn mâu thuẫn:** SRS FR-AUTH-003 không có luồng ngoại lệ cho tài khoản bị vô hiệu, trong
khi D11 chốt `IsActive = false` → 403 và đã áp cho login (FR-AUTH-002) + refresh (FR-AUTH-004).

**Chốt:**

- Trong `LoginOrRegisterWithGoogleAsync`, sau khi tìm thấy user (theo Google ID hoặc email) và
  **trước** `AddLoginAsync`: `IsActive == false` → `ForbiddenException(AUTH_ACCOUNT_DISABLED)`
  → 403.
- Tài khoản bị vô hiệu **không** được liên kết thêm Google login.
- Áp dụng cả khi tài khoản đã liên kết Google từ trước.

**Sửa SRS:** FR-AUTH-003 — thêm luồng ngoại lệ "tài khoản bị vô hiệu → 403
AUTH_ACCOUNT_DISABLED"; mục HTTP Status Code thêm 403.

## 5. File tạo/sửa theo tầng

### Domain

Không đổi.

### Application

| File | Thay đổi |
|---|---|
| `Common/Interfaces/IIdentityService.cs` | XML doc `LoginOrRegisterWithGoogleAsync`: ném `ForbiddenException` khi tài khoản bị vô hiệu (D11, D37) |

### Infrastructure

| File | Thay đổi |
|---|---|
| `Identity/IdentityService.cs` | **G1:** nhánh `user is not null` → `if (!user.IsActive) throw new ForbiddenException(ErrorCodes.AuthAccountDisabled, …)` trước `AddLoginAsync`. **G4:** gói `CreateAsync` + `AddToRoleAsync` + `AddLoginAsync` trong `IDbContextTransaction`, rollback khi lỗi |

### API

| File | Thay đổi |
|---|---|
| `Endpoints/AuthEndpoints.cs` | Thêm `.ProducesProblem(StatusCodes.Status403Forbidden)` cho `/google` (Scalar) |

### Frontend

| File | Thay đổi |
|---|---|
| `lib/auth/credentials.ts` | `authorizeGoogle` bắt `ApiError`, **không ném** — trả user placeholder mang `googleError: 'unavailable' \| 'disabled' \| 'invalid'` map từ `type` ProblemDetails (502 / 403 / 400) |
| `auth.ts` | Thêm callback `signIn`: có `user.googleError` → trả chuỗi `/auth/login?error=Google<Loại>` (Auth.js v5 hiểu là redirect, không tạo session) |
| `types/next-auth.d.ts` | Khai báo `googleError?` trên `User` |
| `app/auth/login/page.tsx` | Parse `?error=GoogleUnavailable \| GoogleDisabled \| GoogleInvalid` → truyền `googleError` xuống form thay vì boolean `hasGoogleError`; mã lạ → lỗi chung |
| `app/auth/login/LoginForm.tsx` | Toast theo loại: 502 "Google đang gián đoạn, vui lòng thử lại sau" · 403 "Tài khoản đã bị vô hiệu hóa" · 400 "Không xác thực được tài khoản Google (email chưa xác minh hoặc phiên đã hết hạn)" · khác → thông báo chung |

### Test

| File | Case |
|---|---|
| `IntegrationTests/Auth/GoogleLoginTests.cs` | **+2:** `FR-AUTH-003/D11,D37`: tài khoản thủ công bị vô hiệu + Google cùng email → 403, **không** có bản ghi `AspNetUserLogins` Google · tài khoản đã liên kết Google rồi bị vô hiệu → 403 |
| `UnitTests/Auth/GoogleLoginCommandHandlerTests.cs` *(mới)* | `EmailVerified = false` → `BadRequestException`, không gọi `IIdentityService` · user mới → `EnqueueWelcomeEmail` đúng 1 lần · user cũ → không enqueue · refresh token lưu **hash**, response trả **raw** · `ForbiddenException` từ `IIdentityService` lan thẳng lên, không lưu refresh token |
| `__tests__/lib/auth/credentials.test.ts` | Backend 502 / 403 / 400 → `authorizeGoogle` gán đúng `googleError` |
| `__tests__/lib/auth/callbacks.test.ts` | `signIn` có `googleError` → trả URL redirect đúng; không có → `true` |
| `__tests__/app/auth/login/{page,LoginForm}.test.tsx` | Mỗi mã lỗi → đúng toast; `?error=SessionExpired` không bị nhầm thành lỗi Google |

*G4 khó tái hiện lỗi giữa chừng trong integration test → review code, không viết test riêng.*

### Docs

| File | Thay đổi |
|---|---|
| `docs/decisions.md` | Thêm **D37** (mục 4) |
| `docs/traceability.md` | Dòng FR-AUTH-003: thêm D11, D37 + test mới |
| `docs/permissions.md` | Ghi rõ "tài khoản bị vô hiệu không đăng nhập được bằng bất kỳ phương thức nào" (nếu ma trận có mục này) |

## 6. Thứ tự làm

1. Pull `main`, tạo branch mới từ `main` (vd. `2312800_VCVinh_FR-AUTH-003-va-google`).
2. Thêm D37 vào `decisions.md`.
3. **Test first (BE):** 2 integration test G1 + unit test handler (G3) → chạy, xác nhận test
   G1 **fail**.
4. Sửa `IdentityService` (G1 + G4) → test pass →
   commit `fix(FR-AUTH-003): chặn tài khoản bị vô hiệu đăng nhập qua Google (D11, D37)`.
5. **Test first (FE):** test G2 → fail → code → pass →
   commit `feat(FR-AUTH-003): hiển thị lỗi Google theo mã lỗi backend (D33)`.
6. `dotnet build` (0 warning) · `dotnet test` · `npm run lint && npm run build && npm test`.
7. Cập nhật `traceability.md` (+ `permissions.md`) → commit `docs`.

## 7. Rủi ro

- **Callback `signIn` trả chuỗi redirect với provider OAuth:** cần xác nhận hành vi trên đúng
  phiên bản `next-auth` beta đang cài trước khi viết test G2. Nếu không hoạt động như mong đợi,
  phương án dự phòng: chuyển lời gọi backend từ `profile()` sang callback `jwt` (nhánh
  `account?.provider === 'google'`).
- **G4 transaction:** `UserManager` dùng chung `DbContext` scoped với `IUnitOfWork` — kiểm tra
  `BeginTransactionAsync` không xung đột với execution strategy (retry) của Npgsql nếu đã bật.
