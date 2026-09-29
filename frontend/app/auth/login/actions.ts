'use server';

import { signIn } from '@/auth';
import type { ProblemDetails } from '@/lib/api-client';
import type { LoginRequest } from '@/lib/auth/api';
import { BackendAuthError } from '@/lib/auth/credentials';

export type LoginResult = { ok: true } | { ok: false; problem: ProblemDetails };

/**
 * FR-AUTH-002 — đăng nhập + tạo phiên Auth.js trong một bước phía server.
 * Trả ProblemDetails thay vì ném, vì lỗi ném từ server action bị Next che message ở production.
 */
export async function loginAction(body: LoginRequest): Promise<LoginResult> {
  try {
    await signIn('login', { ...body, redirect: false });
    return { ok: true };
  } catch (error) {
    if (error instanceof BackendAuthError) {
      return { ok: false, problem: error.problem };
    }
    throw error;
  }
}
