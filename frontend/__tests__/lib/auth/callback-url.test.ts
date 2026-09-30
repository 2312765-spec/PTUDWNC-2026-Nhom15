import { safeCallbackUrl } from '@/lib/auth/callback-url';

describe('safeCallbackUrl — FR-AUTH-002', () => {
  it.each(['/dashboard', '/dashboard/recipes/new?x=1', '/'])('FR-AUTH-002: giữ đường dẫn nội bộ %s', (url) => {
    expect(safeCallbackUrl(url)).toBe(url);
  });

  it.each([
    ['//evil.com', 'protocol-relative'],
    ['/\\evil.com', 'backslash'],
    ['https://evil.com/dashboard', 'URL tuyệt đối'],
    ['dashboard', 'không bắt đầu bằng /'],
  ])('FR-AUTH-002: chặn open redirect %s (%s) → /dashboard', (url) => {
    expect(safeCallbackUrl(url)).toBe('/dashboard');
  });

  it('FR-AUTH-002: không có / nhiều giá trị → fallback', () => {
    expect(safeCallbackUrl(undefined)).toBe('/dashboard');
    expect(safeCallbackUrl(['/a', '/b'])).toBe('/dashboard');
    expect(safeCallbackUrl(undefined, '/')).toBe('/');
  });
});
