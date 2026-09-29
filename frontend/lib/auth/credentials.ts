import { CredentialsSignin, type User } from 'next-auth';
import { toProblemDetails, type ProblemDetails } from '@/lib/api-client';
import { googleLogin, register } from '@/lib/auth/api';
import type { AuthResponse } from '@/lib/types';

/**
 * Lỗi từ backend khi authorize(). `code` (= Application Error Code) là thứ Auth.js cho
 * lộ ra URL; `problem` đầy đủ chỉ đọc được phía server — signIn() gọi từ server ném lại
 * đúng instance này, nên server action lấy được ProblemDetails.errors (NFR-USE-003).
 */
export class BackendAuthError extends CredentialsSignin {
  readonly problem: ProblemDetails;

  constructor(problem: ProblemDetails) {
    super(problem.title);
    this.code = problem.type;
    this.problem = problem;
  }
}

/** AuthResponseDto → user của Auth.js; token chỉ nằm trong JWT cookie (httpOnly, mã hóa). */
export function toSessionUser(auth: AuthResponse): User {
  return {
    id: auth.user.id,
    email: auth.user.email,
    name: auth.user.displayName,
    image: auth.user.avatarUrl,
    profile: auth.user,
    accessToken: auth.accessToken,
    refreshToken: auth.refreshToken,
    expiresAt: auth.expiresAt,
  };
}

function field(credentials: Partial<Record<string, unknown>>, key: string): string {
  const value = credentials[key];
  return typeof value === 'string' ? value : '';
}

/**
 * FR-AUTH-001 — authorize() của provider "register". Không validate ở đây: backend
 * (RegisterCommandValidator, CONS-008) là nơi quyết định, lỗi trả nguyên về form.
 */
export async function authorizeRegister(
  credentials: Partial<Record<string, unknown>>,
): Promise<User> {
  try {
    const auth = await register({
      email: field(credentials, 'email'),
      password: field(credentials, 'password'),
      displayName: field(credentials, 'displayName'),
    });
    return toSessionUser(auth);
  } catch (error) {
    throw new BackendAuthError(toProblemDetails(error));
  }
}

/**
 * FR-AUTH-003, D9 — profile() của provider Google. Nhận ID Token OIDC mà Auth.js vừa lấy
 * được từ Google (KHÔNG phải access token), gửi cho backend verify + liên kết/tự tạo tài
 * khoản (Author). Backend trả cùng AuthResponseDto với register/login (D24).
 *
 * Khác với authorizeLogin/authorizeRegister: lỗi ném ở đây KHÔNG có kênh trả nguyên
 * ProblemDetails về client — Auth.js bọc thành OAuthCallbackError và chỉ redirect kèm
 * `?error=` chung (luồng OAuth không có chỗ cho response tuỳ biến như authorize()).
 */
export async function authorizeGoogle(idToken: string): Promise<User> {
  const auth = await googleLogin({ idToken });
  return toSessionUser(auth);
}
