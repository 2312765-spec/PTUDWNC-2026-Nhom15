import { render } from '@testing-library/react';
import { notFound } from 'next/navigation';
import RecipeDetailPage, { generateMetadata } from '@/app/(public)/recipes/[slug]/page';
import { ApiError } from '@/lib/categories';
import { getRecipeBySlug } from '@/lib/recipes';
import { buildRecipeJsonLd } from '@/lib/seo/recipeJsonLd';
import { buildRecipeMetadata } from '@/lib/seo/recipeMeta';
import { recipeDetail } from '../../../utils/recipe';

jest.mock('@/lib/recipes', () => ({
  ...jest.requireActual('@/lib/recipes'),
  getRecipeBySlug: jest.fn(),
}));
/** next/image cần cấu hình remotePatterns của Next runtime — trong jsdom thay bằng <img> thường. */
jest.mock('next/image', () => ({
  __esModule: true,
  // eslint-disable-next-line @next/next/no-img-element
  default: (props: { src: string; alt: string }) => <img src={props.src} alt={props.alt} />,
}));
jest.mock('next/navigation', () => ({
  notFound: jest.fn(() => {
    throw new Error('NEXT_NOT_FOUND');
  }),
}));

const mockGetRecipeBySlug = getRecipeBySlug as jest.Mock;
const props = { params: Promise.resolve({ slug: 'pho-bo' }) };

beforeEach(() => {
  jest.clearAllMocks();
});

describe('/recipes/[slug] — xử lý lỗi (FR-RCP-002)', () => {
  it('FR-RCP-002: lấy dữ liệu qua getRecipeBySlug(slug)', async () => {
    mockGetRecipeBySlug.mockResolvedValue(null);

    await expect(RecipeDetailPage(props)).rejects.toThrow('NEXT_NOT_FOUND');

    expect(mockGetRecipeBySlug).toHaveBeenCalledWith('pho-bo');
  });

  it('FR-RCP-002: công thức không tồn tại / không được xem (null) → notFound()', async () => {
    mockGetRecipeBySlug.mockResolvedValue(null);

    await expect(RecipeDetailPage(props)).rejects.toThrow('NEXT_NOT_FOUND');
    expect(notFound).toHaveBeenCalled();
  });

  it('NFR-USE-003: backend lỗi → ném lỗi cho error.tsx, KHÔNG gọi notFound() (không trả 404 giả)', async () => {
    mockGetRecipeBySlug.mockRejectedValue(new ApiError(503, 'UNKNOWN_ERROR'));

    await expect(RecipeDetailPage(props)).rejects.toEqual(new ApiError(503, 'UNKNOWN_ERROR'));
    expect(notFound).not.toHaveBeenCalled();
  });

  it('NFR-USE-003: metadata khi backend lỗi cũng ném lỗi, không gắn title "Không tìm thấy"', async () => {
    mockGetRecipeBySlug.mockRejectedValue(new ApiError(500, 'INTERNAL_ERROR'));

    await expect(generateMetadata(props)).rejects.toEqual(new ApiError(500, 'INTERNAL_ERROR'));
  });

  it('FR-RCP-002 / NFR-SEO-002: metadata khi không tồn tại → title "Không tìm thấy công thức" (đuôi do template layout thêm, không lặp)', async () => {
    mockGetRecipeBySlug.mockResolvedValue(null);

    await expect(generateMetadata(props)).resolves.toEqual({ title: 'Không tìm thấy công thức' });
  });
});

describe('/recipes/[slug] — SEO (NFR-SEO-001/002)', () => {
  const original = process.env.NEXT_PUBLIC_SITE_URL;

  beforeEach(() => {
    process.env.NEXT_PUBLIC_SITE_URL = 'https://culinaryblog.vn';
  });

  afterEach(() => {
    process.env.NEXT_PUBLIC_SITE_URL = original;
  });

  it('NFR-SEO-002: generateMetadata dùng buildRecipeMetadata (title không lặp đuôi, canonical, OG, Twitter, robots)', async () => {
    const recipe = recipeDetail();
    mockGetRecipeBySlug.mockResolvedValue(recipe);

    const metadata = await generateMetadata(props);

    expect(metadata).toEqual(buildRecipeMetadata(recipe));
    expect(String(metadata.title)).not.toContain('Culinary Blog');
  });

  it('NFR-SEO-001: trang nhúng đúng một JSON-LD Recipe', async () => {
    const recipe = recipeDetail();
    mockGetRecipeBySlug.mockResolvedValue(recipe);

    const { container } = render(await RecipeDetailPage(props));

    const scripts = container.querySelectorAll('script[type="application/ld+json"]');
    expect(scripts).toHaveLength(1);
    expect(JSON.parse(scripts[0].innerHTML)).toEqual(buildRecipeJsonLd(recipe, 'https://culinaryblog.vn'));
  });

  it('NFR-SEO-001: mỗi bước có id="buoc-n" khớp HowToStep.url', async () => {
    mockGetRecipeBySlug.mockResolvedValue(recipeDetail());

    const { container } = render(await RecipeDetailPage(props));

    expect(container.querySelector('#buoc-1')).toHaveTextContent('Sơ chế xương');
    expect(container.querySelector('#buoc-2')).toHaveTextContent('Ninh nước dùng');
  });
});
