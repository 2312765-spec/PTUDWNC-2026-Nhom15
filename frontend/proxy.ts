/**
 * FR-AUTH-004 — chạy callback jwt của Auth.js TRƯỚC khi render, ở nơi ghi được cookie.
 *
 * Server Component không ghi được cookie: nếu access token chỉ được refresh trong auth() lúc
 * render, backend đã rotation mà RT mới không được lưu → lần sau gửi RT cũ → reuse detection
 * (D35) đăng xuất người dùng khỏi mọi thiết bị. Chạy ở đây thì cookie mới đi kèm response.
 *
 * FR-AUTH-006/D58 — callback `authorized` (lib/auth/callbacks.ts) cũng chạy ở đây để chặn
 * /dashboard/* khi chưa đăng nhập. Chỉ để UX, backend vẫn tự kiểm tra quyền.
 */
export { auth as proxy } from '@/auth';

export const config = {
  // Bỏ endpoint của Auth.js (tự đọc/ghi phiên) và tài nguyên tĩnh — không cần phiên, và mỗi
  // lần chạy proxy là một lần giải mã cookie.
  matcher: [
    '/((?!api/auth|_next/static|_next/image|favicon\\.ico|.*\\.(?:png|jpe?g|gif|webp|avif|svg|ico)$).*)',
  ],
};
