import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import SearchResults from '@/app/(public)/search/SearchResults';
import { ApiError } from '@/lib/categories';
import { searchRecipes } from '@/lib/recipes';
import type { PagedResult, RecipeSummaryDto } from '@/lib/types';
import { mockPush } from '../../../jest.setup';
import { renderWithProviders } from '../../utils/render';

jest.mock('@/lib/recipes', () => ({
  ...jest.requireActual('@/lib/recipes'),
  searchRecipes: jest.fn(),
}));

const mockSearch = searchRecipes as jest.MockedFunction<typeof searchRecipes>;

const recipe = (overrides: Partial<RecipeSummaryDto> = {}): RecipeSummaryDto => ({
  id: 'r1',
  title: 'Phở bò',
  slug: 'pho-bo',
  description: 'Nước dùng ninh xương',
  featuredImageUrl: 'http://localhost:9000/culinary-blog/recipes/pho.webp',
  status: 'Published',
  prepTimeMinutes: 20,
  cookTimeMinutes: 40,
  difficulty: 'Medium',
  authorId: 'a1',
  authorName: null,
  createdAt: '2026-09-30T00:00:00Z',
  ...overrides,
});

const paged = (
  items: RecipeSummaryDto[],
  extra: Partial<PagedResult<RecipeSummaryDto>> = {},
): PagedResult<RecipeSummaryDto> => ({
  items,
  totalCount: items.length,
  page: 1,
  pageSize: 12,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
  ...extra,
});

beforeEach(() => mockSearch.mockReset());

describe('SearchResults — FR-SRCH-001', () => {
  it('FR-SRCH-001: hiển thị ảnh, tổng thời gian và độ khó từ đúng field của RecipeSummaryDto', async () => {
    mockSearch.mockResolvedValue(paged([recipe()]));

    renderWithProviders(await SearchResults({ q: 'pho', page: 1 }));

    expect(mockSearch).toHaveBeenCalledWith('pho', 1, 12);
    expect(screen.getByRole('status')).toHaveTextContent('Tìm thấy 1 kết quả cho "pho"');
    expect(screen.getByRole('link', { name: /Phở bò/ })).toHaveAttribute('href', '/recipes/pho-bo');
    expect(screen.getByRole('img', { name: 'Phở bò' })).toBeInTheDocument();
    expect(screen.getByText('60 phút')).toBeInTheDocument();
    expect(screen.getByText('Trung bình')).toBeInTheDocument();
  });

  it('FR-SRCH-001: recipe không có ảnh → placeholder, không vỡ trang', async () => {
    mockSearch.mockResolvedValue(paged([recipe({ featuredImageUrl: null })]));

    renderWithProviders(await SearchResults({ q: 'pho', page: 1 }));

    expect(screen.getByText('Chưa có hình ảnh')).toBeInTheDocument();
  });

  it('FR-SRCH-001: không có kết quả → thông báo trạng thái rỗng', async () => {
    mockSearch.mockResolvedValue(paged([]));

    renderWithProviders(await SearchResults({ q: 'xyz', page: 1 }));

    expect(screen.getByRole('status')).toHaveTextContent('Không tìm thấy công thức nào cho "xyz"');
  });

  it('FR-SRCH-004: nhiều trang → đổi trang bằng URL ?q=&page=', async () => {
    mockSearch.mockResolvedValue(paged([recipe()], { totalCount: 30, totalPages: 3 }));

    renderWithProviders(await SearchResults({ q: 'phở', page: 1 }));
    await userEvent.click(screen.getByRole('button', { name: 'Trang sau' }));

    expect(mockPush).toHaveBeenCalledWith(`/search?q=${encodeURIComponent('phở')}&page=2`);
  });

  it('NFR-USE-003/D4: VALIDATION_ERROR → thông báo theo error code', async () => {
    mockSearch.mockRejectedValue(new ApiError(400, 'VALIDATION_ERROR'));

    renderWithProviders(await SearchResults({ q: 'pho', page: 1 }));

    expect(screen.getByRole('alert')).toHaveTextContent('ít nhất 2 ký tự');
  });

  it('NFR-USE-003: lỗi khác (API sập) → thông báo chung, không lộ chi tiết server', async () => {
    mockSearch.mockRejectedValue(new TypeError('fetch failed'));

    renderWithProviders(await SearchResults({ q: 'pho', page: 1 }));

    expect(screen.getByRole('alert')).toHaveTextContent('Không tìm kiếm được lúc này');
  });
});
