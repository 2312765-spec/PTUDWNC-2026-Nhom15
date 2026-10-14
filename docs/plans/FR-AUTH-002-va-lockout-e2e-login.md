# Plan — FR-AUTH-002: Vá lỗ hổng lockout + E2E đăng nhập

> Người phụ trách: A (Xác thực & Tài khoản) — 2312800@dlu.edu.vn.
> Kế hoạch viết **trước** khi code (bước Plan, mục 9 `CLAUDE.md`).

---

## 1. Phạm vi

FR-AUTH-002 **đã xong** (`traceability.md` ✅): `LoginCommand(+Handler,+Validator)`,
`IdentityService.ValidateCredentialsAsync`, `Auth/LoginTests.cs` (6 integration test),
FE `/auth/login` + Auth.js provider `login` (41 test Jest).

Plan này chỉ **lấp các khoảng trống** phát hiện khi đối chiếu code với SRS + `decisions.md` + NFR:

| # | Khoảng trống | Mức độ | Trạng thái |
|---|---|---|---|
| G1 | Lockout không chặn được dò mật khẩu (oracle 401/423) + bị kéo dài vô hạn | **Cao** — bảo mật | ⬜ |
| G2 | User enumeration qua thời gian phản hồi | Trung bình | ⬜ |
| G3 | Chưa có E2E Playwright cho luồng login (NFR-MAINT-002) | Bắt buộc trước khi đóng NFR | ⬜ |

**Ngoài phạm vi:** rate limiting `/auth/*` 10 req/phút/IP (NFR-SEC-003, D15) — slice riêng.

## 2. Phân tích

### G1 — Lockout vô hiệu

`IdentityService.ValidateCredentialsAsync` kiểm tra **mật khẩu trước, lockout sau** (đúng
thứ tự bước 5→6 của SRS), dẫn tới:

1. **Oracle mật khẩu:** khi tài khoản đang khóa, mật khẩu **sai** → 401, **đúng** → 423.
   Kẻ tấn công vẫn dò tiếp được trong lúc khóa; thấy 423 là biết đã trúng, chờ 15 phút
   rồi đăng nhập. Lockout mất tác dụng.
2. **DoS:** `AccessFailedAsync` vẫn chạy khi đang khóa → mỗi 5 lần sai, Identity đặt lại
   `LockoutEnd = now + 15p`. Kẻ tấn công giữ chủ tài khoản bị khóa vô hạn.

### G2 — Timing enumeration

Email không tồn tại → trả 401 ngay; email tồn tại → chạy PBKDF2 (≥ 100.000 vòng, NFR-SEC-001)
rồi mới 401. Chênh lệch thời gian đủ đo được để biết email nào có tài khoản — trái tinh thần
SRS A1 (chống User Enumeration).

### G3 — E2E

Repo chưa có `e2e/`, `playwright.config`, `@playwright/test`. `team-assignment.md` giao E2E
`register` + `login` cho A. Đây là lần dựng Playwright đầu tiên — cấu hình dùng lại cho các
luồng E2E khác (B: search; C: create recipe, publish).

## 3. Quyết định D-x áp dụng

| D-x | Tóm tắt | Ảnh hưởng |
|---|---|---|
| D17 | Sai 5 lần → lockout 15 phút → 423 `AUTH_ACCOUNT_LOCKED`, `detail` ghi số phút còn lại | Giữ nguyên |
| D11 | `IsActive == false` → 403 `AUTH_ACCOUNT_DISABLED` | Giữ nguyên |
| D4 | Validation → 400 `VALIDATION_ERROR` | Giữ nguyên |
| D23 | Application không đụng `UserManager` | Mọi thay đổi nằm trong `IdentityService` (Infrastructure) |
| **D36** *(cần thêm — chờ chốt)* | Xem mục 4 | Lệch thứ tự bước 5→6 của SRS |

## 4. D36 — đề xuất (thêm vào `decisions.md` trước khi code)

| # | Điểm | Chốt đề xuất |
|---|---|---|
| a | Thứ tự kiểm tra | tìm user → **đang lockout → 423 ngay** (không kiểm tra mật khẩu) → sai mật khẩu → `AccessFailedAsync` + 401 → `IsActive` → reset bộ đếm → 200. Giống `SignInManager.CheckPasswordSignInAsync` của Identity |
| b | Đếm lần sai khi đang khóa | **Không** — không gọi `AccessFailedAsync`, lockout không bị gia hạn |
| c | Email không tồn tại | Vẫn chạy `IPasswordHasher.VerifyHashedPassword` với một hash giả tĩnh để thời gian phản hồi tương đương |
| d | Đánh đổi | 423 tiết lộ rằng một email **đang bị khóa** là có tồn tại — chấp nhận được, nhỏ hơn nhiều so với lộ mật khẩu |

**Sửa SRS (qua CR):** FR-AUTH-002 luồng chính — đổi bước 6 (kiểm tra lockout) lên trước bước 5
(kiểm tra mật khẩu); A3 ghi rõ "không tăng bộ đếm khi đang khóa".

## 5. File tạo/sửa theo tầng

| Tầng | File | Tạo/Sửa | Nội dung |
|---|---|---|---|
| Docs | `docs/decisions.md` | Sửa | Thêm D36 (mục 4) |
| Domain | — | — | Không đổi |
| Application | `Common/Interfaces/IIdentityService.cs` | Sửa | Chỉ XML doc (thứ tự kiểm tra theo D36), chữ ký giữ nguyên |
| Application | `Auth/Commands/Login/*` | — | Không đổi |
| Infrastructure | `Identity/IdentityService.cs` | Sửa | `ValidateCredentialsAsync` theo D36a–c |
| API | — | — | Không đổi (`LockedException` → 423 đã có trong `GlobalExceptionMiddleware`) |
| Test BE | `IntegrationTests/Auth/LoginTests.cs` | Sửa | Thêm 2 test (mục 6). Test cũ "lần thứ 6 đúng mật khẩu → 423" phải vẫn pass |
| Frontend | `app/auth/login/*` | — | Không đổi — `LoginForm` đã xử lý 423/403/401/400 |
| Frontend | `package.json` | Sửa | devDependency `@playwright/test`, script `"e2e": "playwright test"` |
| Frontend | `playwright.config.ts` | Tạo | `testDir: '../e2e'` (theo cấu trúc CLAUDE.md mục 4), `baseURL` `http://localhost:3000`, `webServer` chạy `npm run dev`; giả định API :5000 + hạ tầng Docker đã chạy |
| E2E | `e2e/support/users.ts` | Tạo | Helper tạo user test qua `POST /api/v1/auth/register` (email ngẫu nhiên) — không phụ thuộc seed |
| E2E | `e2e/auth/login.spec.ts` | Tạo | 3 kịch bản (mục 6) |
| Docs | `docs/traceability.md` | Sửa | Dòng FR-AUTH-002: thêm D36, test mới, E2E; bỏ "(E2E Playwright chưa có)" |

## 6. Test

**Integration (viết trước, xác nhận fail):**

- `FR-AUTH-002/D36: tài khoản đang khóa + sai mật khẩu → 423 AUTH_ACCOUNT_LOCKED (không phải 401)`
- `FR-AUTH-002/D36: tài khoản đang khóa, sai thêm 5 lần → LockoutEnd không bị gia hạn`

**E2E Playwright (`e2e/auth/login.spec.ts`):**

- Đăng nhập đúng → chuyển tới `/dashboard` + toast "Đăng nhập thành công"
- Sai mật khẩu → alert "Email hoặc mật khẩu không đúng." (thông báo chung)
- Vào `/dashboard` khi chưa đăng nhập → `/auth/login?callbackUrl=...` → đăng nhập → quay về đúng trang

## 7. Thứ tự thực hiện

1. Chốt + ghi D36 vào `decisions.md`.
2. Viết 2 integration test → chạy, xác nhận **fail**.
3. Sửa `IdentityService.ValidateCredentialsAsync` → toàn bộ `LoginTests` pass, 0 warning.
4. Commit `fix(FR-AUTH-002): kiem tra lockout truoc mat khau, chong do mat khau (D36)`.
5. Dựng Playwright + `login.spec.ts` → chạy pass.
6. Cập nhật `traceability.md` → commit `test(FR-AUTH-002): them E2E Playwright luong dang nhap`.
