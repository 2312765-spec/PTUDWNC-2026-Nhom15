import { AxiosError } from 'axios';
import type { JWT } from 'next-auth/jwt';
import { refresh } from '@/lib/auth/api';
import type { AuthResponse } from '@/lib/types';

/** Refresh sớm hơn hạn thật một chút — tránh token hết hạn giữa đường tới backend. */
export const REFRESH_SKEW_MS = 60_000;

/**
 * Thời gian giữ kết quả một lần refresh thành công, tính theo RT CŨ.
 *
 * D35: RT đã bị thu hồi mà gửi lại → backend revoke MỌI RT của user. Trong một request, proxy
 * refresh xong và gửi cookie mới về trình duyệt, nhưng Server Component của CHÍNH request đó
 * vẫn đọc cookie cũ; các request song song cũng vậy. Những lần đó phải nhận lại kết quả đã có,
 * không được gửi RT cũ lên backend lần nữa.
 */
export const REFRESH_RESULT_TTL_MS = 30_000;

interface RefreshEntry {
  promise: Promise<AuthResponse>;
  /** Chỉ có khi đã thành công — mốc để hết hạn kết quả. */
  settledAt?: number;
}

/**
 * Đặt trên globalThis, không phải biến module: proxy.ts có thể được bundle riêng với phần render
 * (docs Next 16 — proxy.md) nên mỗi bên có một bản module riêng, nhưng cùng một tiến trình Node.
 * Giới hạn: chỉ đúng khi frontend chạy 1 instance (xem docs/plans/FR-AUTH-004-frontend-tu-refresh.md).
 */
const cacheKey = Symbol.for('culinary-blog.auth.refresh-cache');
const globalStore = globalThis as typeof globalThis & { [cacheKey]?: Map<string, RefreshEntry> };
const cache = (globalStore[cacheKey] ??= new Map<string, RefreshEntry>());

/** FR-AUTH-004 — access token còn hạn dưới REFRESH_SKEW_MS (hoặc không đọc được hạn) → cần refresh. */
export function isAccessTokenExpiring(expiresAt: string | undefined, now = Date.now()): boolean {
  if (!expiresAt) return true;
  const expiresAtMs = Date.parse(expiresAt);
  if (Number.isNaN(expiresAtMs)) return true;
  return expiresAtMs - now < REFRESH_SKEW_MS;
}

function pruneExpired(now: number): void {
  for (const [key, entry] of cache) {
    if (entry.settledAt !== undefined && now - entry.settledAt >= REFRESH_RESULT_TTL_MS) {
      cache.delete(key);
    }
  }
}

/** Gộp các lần refresh trùng RT: cùng một promise khi đang chạy và trong REFRESH_RESULT_TTL_MS sau đó. */
function refreshOnce(refreshToken: string): Promise<AuthResponse> {
  pruneExpired(Date.now());

  const existing = cache.get(refreshToken);
  if (existing) return existing.promise;

  const entry: RefreshEntry = {
    promise: refresh({ refreshToken }).then(
      (response) => {
        entry.settledAt = Date.now();
        return response;
      },
      (error: unknown) => {
        // Lỗi không được giữ lại — lần sau phải gọi backend thật.
        cache.delete(refreshToken);
        throw error;
      },
    ),
  };
  cache.set(refreshToken, entry);
  return entry.promise;
}

/** Backend đã từ chối RT (401 hết hạn/thu hồi/không hợp lệ, 403 tài khoản bị vô hiệu hóa — D11, D35). */
function isRejectedByBackend(error: unknown): boolean {
  const status = error instanceof AxiosError ? error.response?.status : undefined;
  return status === 401 || status === 403;
}

/**
 * FR-AUTH-004 — đổi RT lấy cặp token mới.
 * - Thành công → token mới (và profile mới nhất từ backend).
 * - Backend từ chối (401/403) → `error: 'RefreshTokenError'`, bỏ cả hai token: phiên đã chết,
 *   SessionExpiryWatcher sẽ đăng xuất người dùng.
 * - Lỗi mạng / 5xx → giữ token nguyên để lần sau thử lại: backend tạm thời không phản hồi không
 *   phải lý do để đăng xuất người dùng.
 */
export async function refreshAccessToken(token: JWT): Promise<JWT> {
  if (!token.refreshToken) {
    return { ...token, accessToken: undefined, error: 'RefreshTokenError' };
  }

  try {
    const response = await refreshOnce(token.refreshToken);
    return {
      ...token,
      profile: response.user,
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
      expiresAt: response.expiresAt,
      error: undefined,
    };
  } catch (error) {
    if (isRejectedByBackend(error)) {
      return {
        ...token,
        accessToken: undefined,
        refreshToken: undefined,
        error: 'RefreshTokenError',
      };
    }
    return token;
  }
}
