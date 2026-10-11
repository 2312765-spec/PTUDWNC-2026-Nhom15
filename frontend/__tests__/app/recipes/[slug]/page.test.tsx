import { notFound } from 'next/navigation';
import RecipeDetailPage, { generateMetadata } from '@/app/(public)/recipes/[slug]/page';
import { ApiError } from '@/lib/categories';
import { getRecipeBySlug } from '@/lib/recipes';

jest.mock('@/lib/recipes', () => ({
  ...jest.requireActual('@/lib/recipes'),
  getRecipeBySlug: jest.fn(),
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

  it('FR-RCP-002: metadata khi không tồn tại → title "Không tìm thấy công thức"', async () => {
    mockGetRecipeBySlug.mockResolvedValue(null);

    await expect(generateMetadata(props)).resolves.toEqual({ title: 'Không tìm thấy công thức | Culinary Blog' });
  });
});
