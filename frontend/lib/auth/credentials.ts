import { CredentialsSignin, type User } from 'next-auth';
import { toProblemDetails, type ProblemDetails } from '@/lib/api-client';
import { register } from '@/lib/auth/api';
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
