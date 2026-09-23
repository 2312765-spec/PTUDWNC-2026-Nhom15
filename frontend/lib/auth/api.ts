import { apiClient } from '@/lib/api-client';
import type { AuthResponse } from '@/lib/types';

/** D5 — wire contract của POST /auth/register. */
export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
}

/**
 * Gọi từ server (authorize() của Auth.js) — trong container, "localhost:5000" của
 * NEXT_PUBLIC_API_URL không trỏ tới API, nên ưu tiên API_INTERNAL_URL (server-only).
 */
function serverBaseUrl(): string | undefined {
  return typeof window === 'undefined' ? process.env.API_INTERNAL_URL : undefined;
}

/** FR-AUTH-001 — 201 trả AuthResponseDto đầy đủ (D24, auto-login). */
export async function register(body: RegisterRequest): Promise<AuthResponse> {
  const baseURL = serverBaseUrl();
  const { data } = await apiClient.post<AuthResponse>(
    '/auth/register',
    body,
    baseURL ? { baseURL } : undefined,
  );
  return data;
}
