import { headers } from 'next/headers';

/**
 * NFR-SEC-003/D62 — IP thật của người dùng, để gửi kèm các lời gọi server → API (authorize,
 * refresh, logout). Các lời gọi đó đi qua API_INTERNAL_URL, không qua Nginx, nên thiếu IP này
 * thì API chỉ thấy IP container frontend: cả site chung một bộ đếm 10 req/phút của /auth/*.
 *
 * Chỉ đọc `X-Real-IP` — Nginx ghi đè bằng `$remote_addr` và frontend không publish cổng (D60),
 * nên client không giả được. KHÔNG đọc `X-Forwarded-For`: phần đầu chuỗi do client tự gửi.
 *
 * Chỉ dùng ở server. Ngoài request scope (build, test) → undefined: API đếm theo IP kết nối.
 */
export async function getClientIp(): Promise<string | undefined> {
  try {
    const ip = (await headers()).get('x-real-ip')?.trim();
    return ip || undefined;
  } catch {
    return undefined;
  }
}
