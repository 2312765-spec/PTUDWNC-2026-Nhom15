import NextAuth from 'next-auth';
import Credentials from 'next-auth/providers/credentials';
import { authorizeRegister } from '@/lib/auth/credentials';

/**
 * Auth.js v5 — chủ sở hữu: A. Slice S2.
 *
 * Phiên dạng JWT (CONS-004 stateless): access/refresh token của backend nằm trong cookie
 * session httpOnly đã mã hóa bằng AUTH_SECRET, không bao giờ vào JS trình duyệt.
 * Session gửi xuống client chỉ có hồ sơ (D5) + accessToken.
 *
 * TODO(S2 — A): provider "login" (FR-AUTH-002), tự refresh khi accessToken hết hạn
 * (FR-AUTH-004), signOut gọi /auth/logout (FR-AUTH-005). TODO(S9 — A): Google (FR-AUTH-003).
 */
export const { handlers, auth, signIn, signOut } = NextAuth({
  session: {
    strategy: 'jwt',
    // CONS-004 — sống bằng refresh token (7 ngày)
    maxAge: 7 * 24 * 60 * 60,
  },
  pages: { signIn: '/auth/login', newUser: '/dashboard' },
  providers: [
    Credentials({
      id: 'register',
      name: 'Đăng ký',
      credentials: { email: {}, password: {}, displayName: {} },
      authorize: authorizeRegister,
    }),
  ],
  callbacks: {
    jwt({ token, user }) {
      if (user?.profile) {
        token.profile = user.profile;
        token.accessToken = user.accessToken;
        token.refreshToken = user.refreshToken;
        token.expiresAt = user.expiresAt;
      }
      return token;
    },
    session({ session, token }) {
      session.profile = token.profile;
      session.accessToken = token.accessToken;
      return session;
    },
  },
});
