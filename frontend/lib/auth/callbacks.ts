import type { Session, User } from 'next-auth';
import type { JWT } from 'next-auth/jwt';
import { isAccessTokenExpiring, refreshAccessToken } from '@/lib/auth/refresh';
import type { UserProfile } from '@/lib/types';

/**
 * Callback `jwt` của Auth.js — chạy mỗi lần đọc phiên (proxy, auth(), /api/auth/session).
 * - Vừa đăng nhập (FR-AUTH-001/002/003): chép token backend vào JWT cookie.
 * - FR-AUTH-004: access token sắp hết hạn → refresh (rotation). Phiên đã hỏng thì để nguyên,
 *   không gọi backend lặp lại.
 */
export async function jwtCallback({
  token,
  user,
  trigger,
  session,
}: {
  token: JWT;
  user?: User;
  trigger?: 'signIn' | 'signUp' | 'update';
  session?: { profile?: Partial<UserProfile> };
}): Promise<JWT> {
  // FR-AUTH-007 — client gọi useSession().update() sau khi sửa hồ sơ để header đổi tên ngay.
  // Dữ liệu từ client không đáng tin: chỉ chép 3 trường hiển thị, tuyệt đối không nhận id/email/roles.
  if (trigger === 'update' && token.profile && session?.profile) {
    const { displayName, avatarUrl, bio } = session.profile;
    return {
      ...token,
      profile: {
        ...token.profile,
        ...(typeof displayName === 'string' ? { displayName } : {}),
        ...(avatarUrl === null || typeof avatarUrl === 'string' ? { avatarUrl } : {}),
        ...(bio === null || typeof bio === 'string' ? { bio } : {}),
      },
    };
  }

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
 * Callback `authorized` của Auth.js — chạy trong proxy.ts cho mỗi request (FR-AUTH-006, D61).
 * Chỉ chặn `/dashboard/*` (kể cả `/dashboard/profile`) khi chưa đăng nhập; phiên đã hỏng
 * (session.error, FR-AUTH-004) coi như chưa đăng nhập. Chỉ để UX — backend vẫn tự kiểm tra
 * quyền ở Application Layer (permissions.md mục 4). Kiểm tra role là việc của từng trang.
 *
 * Tự dựng redirect thay vì trả `false`: Auth.js sẽ gắn callbackUrl là URL tuyệt đối, mà
 * `safeCallbackUrl` chỉ nhận đường dẫn nội bộ nên sẽ bỏ nó và quay về /dashboard.
 */
export function authorizedCallback({
  auth,
  request,
}: {
  auth: Session | null;
  request: { nextUrl: URL };
}): boolean | Response {
  const { pathname, search } = request.nextUrl;
  const isProtected = pathname === '/dashboard' || pathname.startsWith('/dashboard/');
  if (!isProtected) return true;
  if (auth?.profile && !auth.error) return true;

  const loginUrl = new URL('/auth/login', request.nextUrl);
  loginUrl.searchParams.set('callbackUrl', `${pathname}${search}`);
  return Response.redirect(loginUrl, 307);
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
