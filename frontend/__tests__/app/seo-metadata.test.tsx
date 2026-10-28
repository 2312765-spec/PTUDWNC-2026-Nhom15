/**
 * @jest-environment node
 */
// Chỉ đọc metadata, không render — `next/cache` (app/categories/page.tsx) cần TextEncoder mà jsdom không có.
import { metadata as rootMetadata } from '@/app/layout';
import { metadata as homeMetadata } from '@/app/page';
import { metadata as recipesMetadata } from '@/app/(public)/recipes/page';
import { generateMetadata as searchMetadata } from '@/app/(public)/search/page';
import { metadata as categoriesMetadata } from '@/app/categories/page';
import { generateMetadata as categoryMetadata } from '@/app/categories/[slug]/page';
import { getCategoryBySlug } from '@/lib/categories';
import { getSiteUrl } from '@/lib/seo/site';

jest.mock('@/lib/categories', () => ({
  ...jest.requireActual('@/lib/categories'),
  getCategoryBySlug: jest.fn(),
}));

const mockGetCategoryBySlug = getCategoryBySlug as jest.Mock;

/** NFR-SEO-002 — mặc định chung ở root layout; trang con chỉ ghi đè phần riêng. */
describe('app/layout — metadata mặc định', () => {
  it('NFR-SEO-002: metadataBase = NEXT_PUBLIC_SITE_URL → canonical/og:url tương đối thành tuyệt đối', () => {
    expect(rootMetadata.metadataBase?.toString()).toBe(`${getSiteUrl()}/`);
  });

  it('NFR-SEO-002: title template "%s | Culinary Blog"', () => {
    expect(rootMetadata.title).toEqual(expect.objectContaining({ template: '%s | Culinary Blog' }));
  });

  it('NFR-SEO-002: Open Graph mặc định (siteName, locale vi_VN, type website) + Twitter summary_large_image', () => {
    expect(rootMetadata.openGraph).toEqual(
      expect.objectContaining({ siteName: 'Culinary Blog', locale: 'vi_VN', type: 'website' }),
    );
    expect(rootMetadata.twitter).toEqual(expect.objectContaining({ card: 'summary_large_image' }));
  });
});

/** D61.5 — canonical không kèm query param (NFR-SEO-004: query chỉ dùng cho filter/sort/paging). */
describe('canonical các trang công khai (NFR-SEO-002, D61.5)', () => {
  it.each([
    ['/', homeMetadata],
    ['/recipes', recipesMetadata],
    ['/categories', categoriesMetadata],
  ])('NFR-SEO-002: %s có canonical', (path, metadata) => {
    expect(metadata.alternates?.canonical).toBe(path);
  });

  it('NFR-SEO-002: /categories/[slug] canonical theo slug', async () => {
    mockGetCategoryBySlug.mockResolvedValue({
      category: { id: 'c1', name: 'Món nước', slug: 'mon-nuoc', description: null },
      recipes: { items: [], page: 1, pageSize: 12, totalCount: 0, totalPages: 0 },
    });

    const metadata = await categoryMetadata({
      params: Promise.resolve({ slug: 'mon-nuoc' }),
      searchParams: Promise.resolve({ page: '3' }),
    } as never);

    expect(metadata.alternates?.canonical).toBe('/categories/mon-nuoc');
  });

  it('D61.4: /search?q=... → noindex, follow; canonical /search không kèm q', async () => {
    const metadata = await searchMetadata({ searchParams: Promise.resolve({ q: 'phở', page: '2' }) });

    expect(metadata.robots).toEqual({ index: false, follow: true });
    expect(metadata.alternates?.canonical).toBe('/search');
  });
});
