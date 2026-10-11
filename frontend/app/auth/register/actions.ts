'use server';

import { signIn } from '@/auth';
import type { ProblemDetails } from '@/lib/api-client';
import type { RegisterRequest } from '@/lib/auth/api';
import { BackendAuthError } from '@/lib/auth/credentials';

export type RegisterResult = { ok: true } | { ok: false; problem: ProblemDetails };

/**
 * FR-AUTH-001 — đăng ký + auto-login (D24) trong một bước phía server.
 * Trả ProblemDetails thay vì ném, vì lỗi ném từ server action bị Next che message ở production.
 */
export async function registerAction(body: RegisterRequest): Promise<RegisterResult> {
  try {
    await signIn('register', { ...body, redirect: false });
    return { ok: true };
  } catch (error) {
    if (error instanceof BackendAuthError) {
      return { ok: false, problem: error.problem };
    }
    throw error;
  }
}
