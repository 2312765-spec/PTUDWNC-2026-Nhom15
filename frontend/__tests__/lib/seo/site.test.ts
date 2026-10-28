import { absoluteUrl, getSiteUrl } from '@/lib/seo/site';

/** NFR-SEO-002 / D52 — gốc URL tuyệt đối của site, dùng cho canonical, og:url, JSON-LD, robots.txt. */
describe('lib/seo/site', () => {
  const original = process.env.NEXT_PUBLIC_SITE_URL;

  afterEach(() => {
    process.env.NEXT_PUBLIC_SITE_URL = original;
  });

  it('D52: đọc NEXT_PUBLIC_SITE_URL, bỏ dấu / cuối', () => {
    process.env.NEXT_PUBLIC_SITE_URL = 'https://culinaryblog.vn//';

    expect(getSiteUrl()).toBe('https://culinaryblog.vn');
  });

  it('D52: chưa cấu hình → mặc định http://localhost:3000', () => {
    delete process.env.NEXT_PUBLIC_SITE_URL;

    expect(getSiteUrl()).toBe('http://localhost:3000');
  });

  it('NFR-SEO-002: đường dẫn tương đối → ghép với gốc site', () => {
    process.env.NEXT_PUBLIC_SITE_URL = 'https://culinaryblog.vn';

    expect(absoluteUrl('/recipes/pho-bo')).toBe('https://culinaryblog.vn/recipes/pho-bo');
    expect(absoluteUrl('recipes/pho-bo')).toBe('https://culinaryblog.vn/recipes/pho-bo');
  });

  it('NFR-SEO-001: URL đã tuyệt đối (ảnh MinIO — D58) giữ nguyên', () => {
    process.env.NEXT_PUBLIC_SITE_URL = 'https://culinaryblog.vn';

    expect(absoluteUrl('https://cdn.example/a.jpg')).toBe('https://cdn.example/a.jpg');
    expect(absoluteUrl('http://localhost:9000/culinary-blog/a.jpg')).toBe('http://localhost:9000/culinary-blog/a.jpg');
  });
});
