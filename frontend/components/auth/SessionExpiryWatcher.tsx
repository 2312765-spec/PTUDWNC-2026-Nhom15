'use client';

import { signOut, useSession } from 'next-auth/react';
import { useEffect, useRef } from 'react';

/**
 * FR-AUTH-004 — refresh token hết hạn / bị thu hồi / tài khoản bị vô hiệu hóa (callback jwt gắn
 * `session.error`) → đăng xuất và đưa về trang đăng nhập, trang đó báo "phiên đã hết hạn".
 * Không render gì.
 */
export function SessionExpiryWatcher() {
  const { data: session } = useSession();
  const signingOut = useRef(false);
  const error = session?.error;

  useEffect(() => {
    if (error !== 'RefreshTokenError' || signingOut.current) return;
    signingOut.current = true;
    void signOut({ redirectTo: '/auth/login?error=SessionExpired' });
  }, [error]);

  return null;
}
