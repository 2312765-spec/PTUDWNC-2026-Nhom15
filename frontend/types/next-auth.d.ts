import type { UserProfile } from '@/lib/types';

/** FR-AUTH-004 — refresh token hết hạn/bị thu hồi/tài khoản bị vô hiệu hóa: phiên đã chết. */
export type SessionError = 'RefreshTokenError';

/** Mở rộng kiểu của Auth.js v5 cho dữ liệu backend (D5, D24). */
declare module 'next-auth' {
  interface User {
    profile?: UserProfile;
    accessToken?: string;
    refreshToken?: string;
    expiresAt?: string;
  }

  interface Session {
    profile?: UserProfile;
    accessToken?: string;
    /** FR-AUTH-004 — có giá trị thì SessionExpiryWatcher đăng xuất người dùng. */
    error?: SessionError;
  }
}

declare module '@auth/core/jwt' {
  interface JWT {
    profile?: UserProfile;
    accessToken?: string;
    /** Chỉ nằm trong cookie đã mã hóa — KHÔNG copy sang Session. */
    refreshToken?: string;
    expiresAt?: string;
    /** FR-AUTH-004 — xem SessionError. */
    error?: SessionError;
  }
}
