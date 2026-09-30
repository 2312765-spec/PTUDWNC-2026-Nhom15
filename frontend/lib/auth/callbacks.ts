import type { Session, User } from 'next-auth';
import type { JWT } from 'next-auth/jwt';
import { isAccessTokenExpiring, refreshAccessToken } from '@/lib/auth/refresh';

/**
 * Callback `jwt` của Auth.js — chạy mỗi lần đọc phiên (proxy, auth(), /api/auth/session).
 * - Vừa đăng nhập (FR-AUTH-001/002/003): chép token backend vào JWT cookie.
 * - FR-AUTH-004: access token sắp hết hạn → refresh (rotation). Phiên đã hỏng thì để nguyên,
 *   không gọi backend lặp lại.
 */
export async function jwtCallback({ token, user }: { token: JWT; user?: User }): Promise<JWT> {
  if (user?.profile) {
    return {
      ...token,
      profile: user.profile,
      accessToken: user.accessToken,
      refreshToken: user.refreshToken,
      expiresAt: user.expiresAt,
      error: undefined,
    };
  }

  if (token.error || !token.refreshToken) return token;
  if (!isAccessTokenExpiring(token.expiresAt)) return token;

  return refreshAccessToken(token);
}

/**
 * Callback `session` — thứ duy nhất client nhìn thấy. NFR-SEC-002: refreshToken chỉ ở trong
 * JWT cookie (httpOnly, mã hóa), không bao giờ chép sang Session.
 */
export function sessionCallback({ session, token }: { session: Session; token: JWT }): Session {
  session.profile = token.profile;
  session.accessToken = token.accessToken;
  session.error = token.error;
  return session;
}
