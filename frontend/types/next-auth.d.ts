import type { UserProfile } from '@/lib/types';

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
  }
}

declare module '@auth/core/jwt' {
  interface JWT {
    profile?: UserProfile;
    accessToken?: string;
    /** Chỉ nằm trong cookie đã mã hóa — KHÔNG copy sang Session. */
    refreshToken?: string;
    expiresAt?: string;
  }
}
