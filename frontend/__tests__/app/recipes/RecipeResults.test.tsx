import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import RecipeResults from '@/app/(public)/recipes/RecipeResults';
import { ApiError } from '@/lib/categories';
import { getRecipes } from '@/lib/recipes';
import type { PagedResult, RecipeSummaryDto } from '@/lib/types';
import { mockPush } from '../../../jest.setup';
import { renderWithProviders } from '../../utils/render';

jest.mock('@/lib/recipes', () => ({
  ...jest.requireActual('@/lib/recipes'),
  getRecipes: jest.fn(),
}));

const mockGetRecipes = getRecipes as jest.MockedFunction<typeof getRecipes>;

const recipe = (overrides: Partial<RecipeSummaryDto> = {}): RecipeSummaryDto => ({
  id: 'r1',
  title: 'Canh chua',
  slug: 'canh-chua',
  description: null,
  featuredImageUrl: null,
  status: 'Published',
  prepTimeMinutes: 10,
  cookTimeMinutes: 25,
  difficulty: 'Expert',
  authorId: 'a1',
  authorName: null,
  createdAt: '2026-10-01T00:00:00Z',
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

beforeEach(() => mockGetRecipes.mockReset());

describe('RecipeResults — FR-RCP-001', () => {
  it('FR-RCP-001: hiển thị card từ đúng field RecipeSummaryDto (thời gian, độ khó Expert)', async () => {
    mockGetRecipes.mockResolvedValue(paged([recipe()]));

    renderWithProviders(await RecipeResults({ filters: { difficulty: 'Expert' } }));

    expect(mockGetRecipes).toHaveBeenCalledWith({ difficulty: 'Expert' }, 12);
    expect(screen.getByRole('status')).toHaveTextContent('Có 1 công thức');
    expect(screen.getByRole('link', { name: /Canh chua/ })).toHaveAttribute('href', '/recipes/canh-chua');
    expect(screen.getByText('35 phút')).toBeInTheDocument();
    expect(screen.getByText('Rất khó')).toBeInTheDocument();
  });

  it('FR-RCP-001: không có kết quả → trạng thái rỗng kèm link xem tất cả', async () => {
    mockGetRecipes.mockResolvedValue(paged([]));

    renderWithProviders(await RecipeResults({ filters: { minServings: '99' } }));

    expect(screen.getByRole('status')).toHaveTextContent('Không có công thức nào khớp với bộ lọc');
    expect(screen.getByRole('link', { name: 'Xem tất cả công thức' })).toHaveAttribute('href', '/recipes');
  });

  it('FR-SRCH-004: đổi trang giữ nguyên bộ lọc trên URL', async () => {
    mockGetRecipes.mockResolvedValue(paged([recipe()], { totalCount: 30, totalPages: 3 }));

    renderWithProviders(await RecipeResults({ filters: { difficulty: 'Easy', sort: 'title' } }));
    await userEvent.click(screen.getByRole('button', { name: 'Trang sau' }));

    expect(mockPush).toHaveBeenCalledWith('/recipes?difficulty=Easy&sort=title&page=2');
  });

  it('NFR-USE-003/D4: VALIDATION_ERROR → báo bộ lọc không hợp lệ', async () => {
    mockGetRecipes.mockRejectedValue(new ApiError(400, 'VALIDATION_ERROR'));

    renderWithProviders(await RecipeResults({ filters: { maxCookTime: '-5' } }));

    expect(screen.getByRole('alert')).toHaveTextContent('Bộ lọc không hợp lệ');
  });

  it('NFR-USE-003: lỗi khác → thông báo chung, không lộ chi tiết server', async () => {
    mockGetRecipes.mockRejectedValue(new TypeError('fetch failed'));

    renderWithProviders(await RecipeResults({ filters: {} }));

    expect(screen.getByRole('alert')).toHaveTextContent('Không tải được danh sách công thức');
  });
});
