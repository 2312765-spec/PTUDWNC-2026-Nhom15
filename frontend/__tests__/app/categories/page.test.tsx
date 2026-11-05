import { render, screen } from '@testing-library/react';
import { unstable_noStore as noStore } from 'next/cache';
import { PHASE_PRODUCTION_BUILD } from 'next/constants';
import CategoriesPage, { revalidate } from '@/app/categories/(list)/page';
import { ApiError, getCategories } from '@/lib/categories';
import type { CategoryDto } from '@/lib/types';

// `next/cache` thật cần TextEncoder mà jsdom không có
jest.mock('next/cache', () => ({ unstable_noStore: jest.fn() }));
jest.mock('@/lib/categories', () => ({
  ...jest.requireActual('@/lib/categories'),
  getCategories: jest.fn(),
}));
jest.mock('@/components/categories/DeleteCategoryButton', () => ({
  DeleteCategoryButton: () => null,
}));

const mockGetCategories = getCategories as jest.Mock;
const mockNoStore = noStore as jest.Mock;

const category: CategoryDto = {
  id: 'c1',
  name: 'Món khai vị',
  slug: 'mon-khai-vi',
  description: null,
  recipeCount: 3,
};

describe('/categories (FR-CAT-001, SRS 5.1 ISR 3600)', () => {
  const originalPhase = process.env.NEXT_PHASE;

  afterEach(() => {
    process.env.NEXT_PHASE = originalPhase;
    jest.clearAllMocks();
  });

  it('FR-CAT-001: ISR revalidate = 3600', () => {
    expect(revalidate).toBe(3600);
  });

  it('FR-CAT-001: hiển thị danh mục khi API trả dữ liệu', async () => {
    mockGetCategories.mockResolvedValue([category]);

    render(await CategoriesPage());

    expect(screen.getByRole('heading', { name: 'Món khai vị' })).toBeInTheDocument();
    expect(mockNoStore).not.toHaveBeenCalled();
  });

  it('FR-CAT-001: lúc next build mà API lỗi → render trạng thái lỗi, route vẫn là ISR (không noStore)', async () => {
    process.env.NEXT_PHASE = PHASE_PRODUCTION_BUILD;
    mockGetCategories.mockRejectedValue(new TypeError('fetch failed'));

    render(await CategoriesPage());

    expect(screen.getByRole('alert')).toHaveTextContent('Không tải được danh mục');
    expect(mockNoStore).not.toHaveBeenCalled();
  });

  it('FR-CAT-001: lúc chạy mà API lỗi → noStore + ném lỗi (Next giữ bản cũ, không cache trang lỗi)', async () => {
    delete process.env.NEXT_PHASE;
    mockGetCategories.mockRejectedValue(new ApiError(500, 'UNKNOWN_ERROR'));

    await expect(CategoriesPage()).rejects.toThrow(ApiError);
    expect(mockNoStore).toHaveBeenCalled();
  });
});
