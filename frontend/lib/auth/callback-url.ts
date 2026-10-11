/**
 * FR-AUTH-002 — callbackUrl sau đăng nhập. Chỉ nhận đường dẫn nội bộ ("/dashboard/…"),
 * chặn open redirect ("//evil.com", "https://evil.com", "/\evil.com").
 */
export function safeCallbackUrl(value: unknown, fallback = '/dashboard'): string {
  if (typeof value !== 'string') return fallback;
  if (!value.startsWith('/') || value.startsWith('//') || value.startsWith('/\\')) return fallback;
  return value;
}
