import type { JWT } from 'next-auth/jwt';
import { logout } from '@/lib/auth/api';
import { isAccessTokenExpiring, refreshAccessToken } from '@/lib/auth/refresh';

/**
 * FR-AUTH-005, D40 — thu hồi refresh token ở backend khi người dùng đăng xuất.
 * Gọi từ `events.signOut` của Auth.js (server), nơi duy nhất đọc được RT trong JWT cookie.
 *
 * Best-effort: KHÔNG ném lỗi. Backend tạm thời không phản hồi không được ngăn người dùng đăng
 * xuất ở phía client (cookie vẫn bị xóa; RT còn lại tự hết hạn sau 7 ngày).
 */
export async function revokeRefreshToken(token: JWT): Promise<void> {
  // Không còn RT (phiên đã hỏng — SessionExpiryWatcher) thì không có gì để thu hồi.
  if (!token.refreshToken || token.error) return;

  try {
    // D40-1: endpoint cần access token hợp lệ — sắp hết hạn thì refresh trước. Cookie sắp bị xóa
    // nên không lo RT mới bị bỏ rơi: chính RT mới này sẽ bị thu hồi ngay sau đây.
    let current = token;
    if (isAccessTokenExpiring(token.expiresAt)) {
      current = await refreshAccessToken(token);
    }

    if (!current.accessToken || !current.refreshToken) return;
    await logout({ refreshToken: current.refreshToken }, current.accessToken);
  } catch (error) {
    console.warn('FR-AUTH-005: không thu hồi được refresh token ở backend.', error);
  }
}
