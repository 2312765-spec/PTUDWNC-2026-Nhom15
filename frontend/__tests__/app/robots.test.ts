/**
 * @jest-environment node
 */
import robots from '@/app/robots';

/** NFR-SEO-003, D51/D52 — robots.txt cho phép mọi crawler và khai báo sitemap (thay ping Google). */
describe('app/robots.ts', () => {
  const original = process.env.NEXT_PUBLIC_SITE_URL;

  afterEach(() => {
    process.env.NEXT_PUBLIC_SITE_URL = original;
  });

  it('NFR-SEO-003: cho phép mọi crawler (User-agent: *, Allow: /)', () => {
    const rules = [robots().rules].flat();

    expect(rules).toEqual(expect.arrayContaining([expect.objectContaining({ userAgent: '*', allow: '/' })]));
  });

  it('NFR-SEO-003: chặn /dashboard/ (trang CSR riêng tư, không index)', () => {
    const [rule] = [robots().rules].flat();

    expect([rule.disallow].flat()).toContain('/dashboard/');
  });

  it('D51/D52: khai báo Sitemap tuyệt đối theo NEXT_PUBLIC_SITE_URL, bỏ dấu / cuối', () => {
    process.env.NEXT_PUBLIC_SITE_URL = 'https://culinaryblog.vn/';

    expect(robots().sitemap).toBe('https://culinaryblog.vn/sitemap.xml');
  });

  it('D52: chưa cấu hình NEXT_PUBLIC_SITE_URL → mặc định http://localhost:3000', () => {
    delete process.env.NEXT_PUBLIC_SITE_URL;

    expect(robots().sitemap).toBe('http://localhost:3000/sitemap.xml');
  });
});
