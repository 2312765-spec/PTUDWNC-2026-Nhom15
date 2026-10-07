import NextAuth, { CredentialsSignin } from 'next-auth';
import Credentials from 'next-auth/providers/credentials';
import Google from 'next-auth/providers/google';
import { jwtCallback, sessionCallback } from '@/lib/auth/callbacks';
import { authorizeGoogle, authorizeLogin, authorizeRegister } from '@/lib/auth/credentials';
import { revokeRefreshToken } from '@/lib/auth/logout';

/**
 * Auth.js v5 — chủ sở hữu: A. Slice S2.
 *
 * Phiên dạng JWT (CONS-004 stateless): access/refresh token của backend nằm trong cookie
 * session httpOnly đã mã hóa bằng AUTH_SECRET, không bao giờ vào JS trình duyệt.
 * Session gửi xuống client chỉ có hồ sơ (D5) + accessToken (+ error khi phiên hỏng).
 *
 * FR-AUTH-004: callback jwt tự refresh khi accessToken sắp hết hạn — lib/auth/callbacks.ts,
 * chạy ở proxy.ts để cookie mới được ghi trước khi render.
 *
 * FR-AUTH-005: events.signOut thu hồi refresh token ở backend (lib/auth/logout.ts, D40).
 */
export const { handlers, auth, signIn, signOut } = NextAuth({
  session: {
    strategy: 'jwt',
    // CONS-004 — sống bằng refresh token (7 ngày)
    maxAge: 7 * 24 * 60 * 60,
  },
  pages: { signIn: '/auth/login', newUser: '/dashboard' },
  logger: {
    // Sai mật khẩu / email trùng là luồng bình thường (form tự hiển thị), không phải lỗi hệ thống.
    error(error) {
      if (error instanceof CredentialsSignin) return;
      console.error(error);
    },
  },
  providers: [
    // FR-AUTH-002
    Credentials({
      id: 'login',
      name: 'Đăng nhập',
      credentials: { email: {}, password: {} },
      authorize: authorizeLogin,
    }),
    // FR-AUTH-001
    Credentials({
      id: 'register',
      name: 'Đăng ký',
      credentials: { email: {}, password: {}, displayName: {} },
      authorize: authorizeRegister,
    }),
    // FR-AUTH-003, D9 — profile() nhận (profile Google, tokens) và lấy tokens.id_token,
    // KHÔNG dùng OAuth profile mặc định của Auth.js (không tạo tài khoản qua Adapter).
    Google({
      clientId: process.env.AUTH_GOOGLE_ID,
      clientSecret: process.env.AUTH_GOOGLE_SECRET,
      async profile(_profile, tokens) {
        if (!tokens.id_token) {
          throw new Error('Google không trả về ID Token.');
        }
        return authorizeGoogle(tokens.id_token);
      },
    }),
  ],
  callbacks: {
    jwt: jwtCallback,
    session: sessionCallback,
  },
  events: {
    async signOut(message) {
      if ('token' in message && message.token) await revokeRefreshToken(message.token);
    },
  },
});
