/**
 * NFR-SEC-004/D68 — Content-Security-Policy cho mọi trang Next.js (gắn ở `next.config.ts` → `headers()`).
 *
 * `script-src 'unsafe-inline'` là đánh đổi có chủ ý: App Router chèn script inline (payload RSC), còn CSP
 * dùng nonce buộc mọi trang render động → mất ISR (SRS 5.1). Lớp chống XSS chính vẫn là React escape.
 */
export interface CspOptions {
  /** `next dev` — cần `'unsafe-eval'` (React Refresh) và `ws:` (HMR). */
  isDev: boolean;
  /** NEXT_PUBLIC_API_URL — trình duyệt gọi API (axios) qua origin này. */
  apiUrl: string;
  /** Nguồn ảnh ngoài site được tải thẳng (không qua /_next/image), vd. S3 dev `http://localhost:9000`. */
  imageOrigins: string[];
}

function originOf(url: string): string | null {
  try {
    return new URL(url).origin;
  } catch {
    return null; // URL tương đối hoặc rỗng → cùng nguồn, đã có 'self'
  }
}

export function buildContentSecurityPolicy({ isDev, apiUrl, imageOrigins }: CspOptions): string {
  const apiOrigin = originOf(apiUrl);
  const unique = (values: (string | null)[]) => [...new Set(values.filter((v): v is string => !!v))];

  const directives: Record<string, string[]> = {
    'default-src': ["'self'"],
    'script-src': ["'self'", "'unsafe-inline'", ...(isDev ? ["'unsafe-eval'"] : [])],
    'style-src': ["'self'", "'unsafe-inline'"],
    'img-src': unique(["'self'", 'data:', 'blob:', ...imageOrigins.map(originOf)]),
    'font-src': ["'self'"],
    'connect-src': unique(["'self'", apiOrigin, ...(isDev ? ['ws:'] : [])]),
    'object-src': ["'none'"],
    'base-uri': ["'self'"],
    'frame-ancestors': ["'none'"],
    // FR-AUTH-003 — Auth.js chuyển hướng form đăng nhập Google sang accounts.google.com
    'form-action': ["'self'", 'https://accounts.google.com'],
  };

  return Object.entries(directives)
    .map(([name, values]) => `${name} ${values.join(' ')}`)
    .join('; ');
}
