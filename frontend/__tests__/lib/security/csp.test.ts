import { buildContentSecurityPolicy } from '@/lib/security/csp';

/** Tách "a b; c d" → { a: ['b'], c: ['d'] } để kiểm từng chỉ thị, không phụ thuộc thứ tự. */
function parse(policy: string): Record<string, string[]> {
  return Object.fromEntries(
    policy
      .split(';')
      .map((d) => d.trim())
      .filter(Boolean)
      .map((d) => {
        const [name, ...values] = d.split(/\s+/);
        return [name, values];
      }),
  );
}

const prod = parse(
  buildContentSecurityPolicy({ isDev: false, apiUrl: 'https://culinary.example/api/v1', imageOrigins: [] }),
);
const dev = parse(
  buildContentSecurityPolicy({
    isDev: true,
    apiUrl: 'http://localhost:5000/api/v1',
    imageOrigins: ['http://localhost:9000', 'https://placehold.co'],
  }),
);

describe('buildContentSecurityPolicy (NFR-SEC-004/D68)', () => {
  it('NFR-SEC-004: mặc định chỉ cùng nguồn; chặn plugin, base, nhúng khung', () => {
    expect(prod['default-src']).toEqual(["'self'"]);
    expect(prod['object-src']).toEqual(["'none'"]);
    expect(prod['base-uri']).toEqual(["'self'"]);
    expect(prod['frame-ancestors']).toEqual(["'none'"]);
  });

  it("D68: script prod chỉ 'self' + 'unsafe-inline' (giữ ISR) — KHÔNG 'unsafe-eval'", () => {
    expect(prod['script-src']).toEqual(["'self'", "'unsafe-inline'"]);
  });

  it("D68: dev thêm 'unsafe-eval' (React Refresh) và ws: (HMR)", () => {
    expect(dev['script-src']).toContain("'unsafe-eval'");
    expect(dev['connect-src']).toContain('ws:');
    expect(prod['connect-src']).not.toContain('ws:');
  });

  it('D68: form chỉ gửi về site hoặc Google OAuth (FR-AUTH-003)', () => {
    expect(prod['form-action']).toEqual(["'self'", 'https://accounts.google.com']);
  });

  it('D68: connect-src gồm origin của API (không kèm đường dẫn)', () => {
    expect(prod['connect-src']).toEqual(["'self'", 'https://culinary.example']);
    expect(dev['connect-src']).toContain('http://localhost:5000');
  });

  it('D68: img-src cho phép site, data:, blob: (xem trước ảnh upload) và nguồn ảnh cấu hình', () => {
    expect(prod['img-src']).toEqual(["'self'", 'data:', 'blob:']);
    expect(dev['img-src']).toEqual(expect.arrayContaining(['http://localhost:9000', 'https://placehold.co']));
  });

  it('D68: không có wildcard * ở bất kỳ chỉ thị nào', () => {
    for (const values of [...Object.values(prod), ...Object.values(dev)]) {
      expect(values).not.toContain('*');
    }
  });

  it('D68: một dòng — header HTTP không được chứa xuống dòng', () => {
    const raw = buildContentSecurityPolicy({ isDev: false, apiUrl: 'https://culinary.example/api/v1', imageOrigins: [] });
    expect(raw).not.toMatch(/[\r\n]/);
  });
});
