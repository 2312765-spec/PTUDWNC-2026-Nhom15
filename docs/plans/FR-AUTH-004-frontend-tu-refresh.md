# Plan — FR-AUTH-004 (Frontend): tự làm mới access token

> Người phụ trách: A (Xác thực & Tài khoản) — 2312800@dlu.edu.vn.
> Kế hoạch viết **trước** khi code (bước Plan, mục 9 `CLAUDE.md`).
> Backend đã xong — xem `docs/plans/FR-AUTH-004-lam-moi-token.md` và D35.

---

## 1. Phạm vi

| FR | Nội dung | Trạng thái |
|---|---|---|
| FR-AUTH-004 (FE) | Auth.js v5 tự gọi `POST /api/v1/auth/refresh` khi access token (15 phút) sắp hết hạn; RT hết hạn/bị thu hồi → đăng xuất, đưa về trang đăng nhập | ⬜ Chưa làm |

## 2. Hiện trạng

- `accessToken`, `refreshToken`, `expiresAt` đã nằm trong JWT cookie của Auth.js (httpOnly, mã
  hóa bằng `AUTH_SECRET`) sau khi đăng nhập — `auth.ts` callback `jwt`.
- **Không có chỗ nào gọi `/auth/refresh`.** Hai TODO còn mở: `auth.ts:13`, `lib/api-client.ts:6`.
- Chưa có `proxy.ts`. Next 16: `middleware` đổi tên thành `proxy`, mặc định chạy Node.js runtime
  (`node_modules/next/dist/docs/01-app/03-api-reference/03-file-conventions/proxy.md`).
- Chưa trang nào dùng `session.accessToken` hay gắn `Authorization: Bearer`.

## 3. Rủi ro chính: reuse detection (D35) tự đăng xuất user

Backend (D35): RT **đã bị revoke** mà gửi lại → revoke **mọi** RT của user. Hai tình huống
frontend có thể vô tình gây ra:

- **Server Component không ghi được cookie.** Nếu `auth()` refresh trong Server Component,
  backend đã rotation nhưng RT mới không được lưu → request sau gửi RT cũ → bị coi là reuse →
  user bị đăng xuất khỏi mọi thiết bị.
- **Request song song mang cùng cookie cũ.** Request đầu rotation xong, các request sau vẫn mang
  RT cũ → reuse.

**Cách xử lý:**

- **(a)** Refresh trong `proxy.ts` — chạy trước render và **ghi được cookie**, nên Server
  Component nhìn thấy token mới.
- **(b)** Gộp các lần refresh trùng: `Map<refreshToken, Promise>` giữ kết quả ~30 giây, request
  đồng thời cùng RT dùng chung một lần gọi `/auth/refresh`.
- **Giới hạn:** (b) chỉ đúng khi frontend chạy **1 instance** (Docker Compose hiện tại). Nhiều
  instance → cần "grace period" ở backend, phải thêm **D36** — chưa làm trong kế hoạch này.

## 4. Luồng xử lý

1. Callback `jwt`: access token còn hạn > 60 giây → trả token nguyên trạng, không gọi backend.
2. Sắp hết hạn → `refreshAccessToken(token)` (có gộp trùng):
   - thành công → thay `accessToken`, `refreshToken`, `expiresAt`, `profile`;
   - backend trả **401/403** → `token.error = 'RefreshTokenError'`, xóa `refreshToken`;
   - **lỗi mạng hoặc 5xx** → giữ token nguyên, request sau thử lại (không đăng xuất user chỉ vì
     backend tạm thời không phản hồi).
3. Callback `session` đưa `error` ra client (không bao giờ đưa `refreshToken`).
4. Client component thấy `session.error === 'RefreshTokenError'` → `signOut()` → chuyển tới
   `/auth/login?error=SessionExpired` kèm toast "Phiên đăng nhập đã hết hạn".

## 5. File theo từng phần

### Gọi API — `frontend/lib/auth/`
- **Sửa** `api.ts` — thêm `refresh(refreshToken)` gọi `POST /auth/refresh`, dùng `serverBaseUrl()` như `login`.
- **Tạo** `refresh.ts` — `isAccessTokenExpiring(expiresAt, now, skew = 60s)`; `refreshAccessToken(token)` có gộp trùng, phân biệt 401/403 với lỗi mạng/5xx.
- **Tạo** `callbacks.ts` — tách callback `jwt` và `session` khỏi `auth.ts` để unit test được.

### Auth.js / Next — `frontend/`
- **Sửa** `auth.ts` — dùng callback từ `lib/auth/callbacks.ts`; xóa TODO FR-AUTH-004 (giữ TODO FR-AUTH-005).
- **Tạo** `proxy.ts` — `export { auth as proxy }`; matcher loại `_next`, `api/auth`, file tĩnh. **Chỉ lo refresh, không chặn route.**
- **Sửa** `types/next-auth.d.ts` — thêm `error?: 'RefreshTokenError'` vào `JWT` và `Session`.

### UI
- **Tạo** `components/auth/SessionExpiryWatcher.tsx` (`'use client'`) — có `session.error` thì `signOut` và chuyển hướng.
- **Sửa** `app/providers.tsx` — gắn `SessionExpiryWatcher` bên trong `SessionProvider`.
- **Sửa** `app/auth/login/LoginForm.tsx` — thêm thông báo cho `?error=SessionExpired` (đọc kỹ cách form xử lý `?error=` trước khi sửa).

### Test — Jest + Testing Library
| File | Kiểm tra |
|---|---|
| `__tests__/lib/auth/api.test.ts` (sửa) | `refresh()` gửi đúng URL + body, trả `AuthResponse` |
| `__tests__/lib/auth/refresh.test.ts` (mới) | còn hạn → không gọi API · sắp hết hạn → token mới · 401/403 → `RefreshTokenError` + xóa RT · lỗi mạng/5xx → giữ token · 2 lần gọi đồng thời cùng RT → API chỉ bị gọi **1 lần** |
| `__tests__/lib/auth/callbacks.test.ts` (mới) | `jwt` khi mới đăng nhập / còn hạn / hết hạn · `session` đưa `error` ra và **không** lộ `refreshToken` |
| `__tests__/components/auth/SessionExpiryWatcher.test.tsx` (mới) | có `error` → gọi `signOut` và chuyển tới `/auth/login?error=SessionExpired` · không có lỗi → không làm gì |

### Docs
- `docs/traceability.md` — dòng FR-AUTH-004 ghi thêm phần frontend.

## 6. Ngoài phạm vi (ghi nhận)

- **Interceptor trong `lib/api-client.ts`** (gắn Bearer + thử lại khi 401) — file hợp đồng chung
  do **C** sở hữu, và chưa trang nào gọi API cần xác thực từ client. Để người làm FR đầu tiên cần
  nó (vd. FR-RCP-003) làm, sau khi thống nhất với C.
- **Chặn route `/dashboard`, `/profile` trong `proxy.ts`** — việc của A nhưng không thuộc FR-AUTH-004.
- **E2E Playwright** — dự án chưa dựng thư mục `e2e/`.
- **Grace period ở backend** cho trường hợp nhiều instance frontend — cần D36 nếu triển khai.

## 7. Thứ tự thực hiện

1. Kéo `main` mới nhất về.
2. Viết test (mục 5) → chạy, xác nhận **fail**.
3. Code đến khi test pass; `npm run lint && npm run build` sạch.
4. Cập nhật `traceability.md` → commit `feat(FR-AUTH-004): ...` lên nhánh `2312800_VCVinh_FR-AUTH-004`.
