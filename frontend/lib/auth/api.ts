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

/** FR-AUTH-002 — wire contract của POST /auth/login. */
export interface LoginRequest {
  email: string;
  password: string;
}

/** FR-AUTH-002 — 200 trả AuthResponseDto, cùng shape với register (D24). */
export async function login(body: LoginRequest): Promise<AuthResponse> {
  const baseURL = serverBaseUrl();
  const { data } = await apiClient.post<AuthResponse>(
    '/auth/login',
    body,
    baseURL ? { baseURL } : undefined,
  );
  return data;
}

/** FR-AUTH-003, D9 — wire contract của POST /auth/google: chỉ { idToken }. */
export interface GoogleLoginRequest {
  idToken: string;
}

/** FR-AUTH-003 — 200 trả AuthResponseDto, cùng shape với register/login (D24). */
export async function googleLogin(body: GoogleLoginRequest): Promise<AuthResponse> {
  const baseURL = serverBaseUrl();
  const { data } = await apiClient.post<AuthResponse>(
    '/auth/google',
    body,
    baseURL ? { baseURL } : undefined,
  );
  return data;
}

/** FR-AUTH-004 — wire contract của POST /auth/refresh: RT nằm trong body, không dùng cookie. */
export interface RefreshRequest {
  refreshToken: string;
}

/**
 * FR-AUTH-004 — 200 trả AuthResponseDto với cặp token MỚI (D24, rotation). RT cũ bị thu hồi
 * ngay; gửi lại RT cũ sẽ kích hoạt reuse detection (D35) — gọi qua refreshAccessToken().
 */
export async function refresh(body: RefreshRequest): Promise<AuthResponse> {
  const baseURL = serverBaseUrl();
  const { data } = await apiClient.post<AuthResponse>(
    '/auth/refresh',
    body,
    baseURL ? { baseURL } : undefined,
  );
  return data;
}
