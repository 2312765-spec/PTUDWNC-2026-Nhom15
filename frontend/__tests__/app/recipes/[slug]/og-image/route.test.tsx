import { render, screen } from '@testing-library/react';
import type { ReactElement } from 'react';
import { GET } from '@/app/(public)/recipes/[slug]/og-image/route';
import { ApiError } from '@/lib/categories';
import { getRecipeBySlug } from '@/lib/recipes';
import { loadOgBackground } from '@/lib/seo/ogImage';
import { recipeDetail } from '../../../../utils/recipe';

/** `next/og` chạy Satori + WASM — ngoài phạm vi jsdom. Giữ lại JSX + tùy chọn để kiểm. */
jest.mock('next/og', () => ({
  ImageResponse: jest.fn().mockImplementation(function ImageResponse(
    this: { element: ReactElement; options: unknown },
    element: ReactElement,
    options: unknown,
  ) {
    this.element = element;
    this.options = options;
  }),
}));
jest.mock('@/lib/recipes', () => ({
  ...jest.requireActual('@/lib/recipes'),
  getRecipeBySlug: jest.fn(),
}));
jest.mock('@/lib/seo/ogImage', () => ({
  ...jest.requireActual('@/lib/seo/ogImage'),
  loadOgBackground: jest.fn(),
}));

const mockGetRecipeBySlug = getRecipeBySlug as jest.Mock;
const mockLoadOgBackground = loadOgBackground as jest.Mock;

type Captured = { element: ReactElement; options: { width: number; height: number } };

async function requestImage(): Promise<Captured> {
  // Handler không đọc request; jsdom không có lớp Request.
  const response = await GET({} as Request, { params: Promise.resolve({ slug: 'pho-bo-ha-noi' }) });
  return response as unknown as Captured;
}

beforeEach(() => {
  jest.clearAllMocks();
});

describe('GET /recipes/[slug]/og-image (NFR-SEO-002, D61.1)', () => {
  it('D61.1: có ảnh chính đọc được → nền là ảnh đó, có tên món và tên site, kích thước 1200×630', async () => {
    mockGetRecipeBySlug.mockResolvedValue(recipeDetail());
    mockLoadOgBackground.mockResolvedValue('data:image/jpeg;base64,AAAA');

    const image = await requestImage();

    expect(mockGetRecipeBySlug).toHaveBeenCalledWith('pho-bo-ha-noi');
    expect(mockLoadOgBackground).toHaveBeenCalledWith('https://culinary.example/culinary-blog/recipes/x/a.jpg');
    expect(image.options).toEqual(expect.objectContaining({ width: 1200, height: 630 }));
    const { container } = render(image.element);
    expect(container.querySelector('img')).toHaveAttribute('src', 'data:image/jpeg;base64,AAAA');
    expect(screen.getByText('Phở bò Hà Nội')).toBeInTheDocument();
    expect(screen.getByText('Culinary Blog')).toBeInTheDocument();
  });

  it('D61.1: ảnh không đọc được (WebP/AVIF, lỗi mạng) → thẻ chữ, không có <img>', async () => {
    mockGetRecipeBySlug.mockResolvedValue(recipeDetail());
    mockLoadOgBackground.mockResolvedValue(null);

    const { container } = render((await requestImage()).element);

    expect(container.querySelector('img')).toBeNull();
    expect(screen.getByText('Phở bò Hà Nội')).toBeInTheDocument();
  });

  it('D61.1: công thức không có ảnh → thẻ chữ', async () => {
    mockGetRecipeBySlug.mockResolvedValue(recipeDetail({ images: [] }));
    mockLoadOgBackground.mockResolvedValue(null);

    const image = await requestImage();

    expect(mockLoadOgBackground).toHaveBeenCalledWith(undefined);
    expect(render(image.element).container.querySelector('img')).toBeNull();
  });

  it('D61.1: không tìm thấy / không được xem (null) → thẻ thương hiệu, vẫn 1200×630', async () => {
    mockGetRecipeBySlug.mockResolvedValue(null);

    const image = await requestImage();

    expect(image.options).toEqual(expect.objectContaining({ width: 1200, height: 630 }));
    expect(mockLoadOgBackground).not.toHaveBeenCalled();
    render(image.element);
    expect(screen.getByText('Culinary Blog')).toBeInTheDocument();
  });

  it('D61.1: backend lỗi → thẻ thương hiệu, không ném lỗi (og:image không được 500)', async () => {
    mockGetRecipeBySlug.mockRejectedValue(new ApiError(503, 'UNKNOWN_ERROR'));

    render((await requestImage()).element);

    expect(screen.getByText('Culinary Blog')).toBeInTheDocument();
  });
});
