import axios, { type InternalAxiosRequestConfig } from 'axios';
import { getSession } from 'next-auth/react';

/**
 * HỢP ĐỒNG CHUNG — chủ sở hữu: C (Sprint 0). Cả 4 người dùng.
 *
 * D74 — trên trình duyệt, interceptor gắn `Authorization: Bearer <accessToken>` lấy từ phiên
 * Auth.js. `getSession()` gọi `/api/auth/session`, nơi callback jwt tự refresh access token sắp hết
 * hạn (FR-AUTH-004) — nên không cần tự xử lý 401 ở đây. Phía server (authorize() của Auth.js) tự
 * truyền header khi cần, interceptor bỏ qua.
 */
export const apiClient = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api/v1',
  headers: { 'Content-Type': 'application/json' },
  timeout: 15_000,
});

/** Access token 15 phút (CONS-004) — giữ 30 giây là đủ để gộp các request liên tiếp của một trang. */
const TOKEN_CACHE_MS = 30_000;

let cached: { token: string | undefined; at: number } | null = null;
let inFlight: Promise<string | undefined> | null = null;

/** D74 — access token của phiên hiện tại (undefined khi chưa đăng nhập). Gộp các lời gọi song song. */
export async function getBrowserAccessToken(): Promise<string | undefined> {
  if (cached && Date.now() - cached.at < TOKEN_CACHE_MS) return cached.token;
  inFlight ??= getSession()
    .then((session) => {
      const token = session?.error ? undefined : session?.accessToken;
      cached = { token, at: Date.now() };
      return token;
    })
    .catch(() => undefined)
    .finally(() => {
      inFlight = null;
    });
  return inFlight;
}

/** Chỉ dùng trong test, hoặc sau khi đăng xuất. */
export function resetAccessTokenCache(): void {
  cached = null;
  inFlight = null;
}

export async function attachAccessToken(config: InternalAxiosRequestConfig): Promise<InternalAxiosRequestConfig> {
  if (typeof window === 'undefined' || config.headers.Authorization) return config;
  const token = await getBrowserAccessToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
}

apiClient.interceptors.request.use(attachAccessToken);

// Giữ đường import cũ cho code đã dùng; code client không cần axios nên import thẳng từ problem-details.
export { toProblemDetails, type ProblemDetails } from '@/lib/problem-details';
