# IntegrationTests/Auth — người phụ trách: **A**

Mỗi endpoint cần **ít nhất 1 happy path + 1 error case** (NFR-MAINT-002).

Checklist bắt buộc:
- `RegisterTests` — thành công 201 · email trùng 409 · password yếu 400
- `LoginTests` — thành công 200 · sai mật khẩu 401 **message generic** · 5 lần sai → 423 · `IsActive=false` → 403
- `GoogleLoginTests` — email đã đăng ký thủ công → **liên kết**, không tạo tài khoản thứ hai (ca khó nhất) · email mới → tự tạo, role Author · token invalid → 400 (D33, gộp SRS A1+A2) · Google unavailable → 502 (D33, SRS A3) · idToken rỗng → 400 VALIDATION_ERROR
- `RefreshTokenTests` — rotation · **reuse detection** (token đã revoke → 401 + revoke cả family)
- `LogoutTests` — 204 · token không tồn tại vẫn 204 (idempotent)
- `ProfileTests` — chỉ trả `displayName`/`avatarUrl`/`bio`, **không có** `fullName` (D5)
