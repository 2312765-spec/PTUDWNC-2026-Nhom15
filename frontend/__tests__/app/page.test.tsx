import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import HomePage, { revalidate } from '@/app/(home)/page';
import { ApiError, getCategories } from '@/lib/categories';
import { getLatestRecipes } from '@/lib/recipes';
import type { CategoryDto, PagedResult, RecipeSummaryDto } from '@/lib/types';
import { mockPush } from '../../jest.setup';

jest.mock('@/lib/categories', () => ({
  ...jest.requireActual('@/lib/categories'),
  getCategories: jest.fn(),
}));
jest.mock('@/lib/recipes', () => ({
  ...jest.requireActual('@/lib/recipes'),
  getLatestRecipes: jest.fn(),
}));
jest.mock('@/components/recipes/RecipeCard', () => ({
  __esModule: true,
  default: ({ recipe }: { recipe: RecipeSummaryDto }) => <div data-testid="recipe-card">{recipe.title}</div>,
}));

const mockGetCategories = getCategories as jest.Mock;
const mockGetLatestRecipes = getLatestRecipes as jest.Mock;

const category = (i: number): CategoryDto => ({
  id: `c${i}`,
  name: `Danh mục ${i}`,
  slug: `danh-muc-${i}`,
  description: null,
  recipeCount: i,
});

const recipe = (i: number): RecipeSummaryDto => ({
  id: `r${i}`,
  title: `Món ${i}`,
  slug: `mon-${i}`,
  description: null,
  featuredImageUrl: null,
  status: 'Published',
  prepTimeMinutes: 5,
  cookTimeMinutes: 0,
  difficulty: 'Easy',
  authorId: 'u1',
  authorName: null,
  createdAt: '2026-10-01T00:00:00Z',
});

const paged = (items: RecipeSummaryDto[]): PagedResult<RecipeSummaryDto> => ({
  items,
  totalCount: items.length,
  page: 1,
  pageSize: 6,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
});

async function renderHome() {
  return render(await HomePage());
}

beforeEach(() => {
  jest.resetAllMocks();
  mockGetCategories.mockResolvedValue([category(1), category(2)]);
  mockGetLatestRecipes.mockResolvedValue(paged([recipe(1), recipe(2)]));
});

describe('/ — trang chủ (FR-RCP-001)', () => {
  it('SRS 5.1: trang chủ ISR revalidate 3600', () => {
    expect(revalidate).toBe(3600);
  });

  it('FR-RCP-001: lấy 6 công thức mới nhất và hiển thị bằng RecipeCard dùng chung', async () => {
    await renderHome();

    expect(mockGetLatestRecipes).toHaveBeenCalledWith(6);
    const section = screen.getByRole('region', { name: 'Công thức mới nhất' });
    expect(within(section).getAllByTestId('recipe-card').map((c) => c.textContent)).toEqual(['Món 1', 'Món 2']);
  });

  it('FR-CAT-001: hiện tối đa 6 danh mục, mỗi danh mục link tới /categories/{slug}', async () => {
    mockGetCategories.mockResolvedValue(Array.from({ length: 8 }, (_, i) => category(i + 1)));

    await renderHome();

    const section = screen.getByRole('region', { name: 'Danh mục món ăn' });
    const links = within(section)
      .getAllByRole('link')
      .filter((a) => a.getAttribute('href')?.startsWith('/categories/'));
    expect(links).toHaveLength(6);
    expect(links[0]).toHaveAttribute('href', '/categories/danh-muc-1');
    expect(links[0]).toHaveTextContent('1 công thức');
  });

  it('chưa có công thức → thông báo trống (không phải lỗi)', async () => {
    mockGetLatestRecipes.mockResolvedValue(paged([]));

    await renderHome();

    expect(screen.getByText('Chưa có công thức nào được đăng tải.')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('NFR-USE-003: API công thức lỗi → báo lỗi rõ ràng, không giả làm "chưa có công thức"', async () => {
    mockGetLatestRecipes.mockRejectedValue(new ApiError(500, 'INTERNAL_ERROR'));

    await renderHome();

    const section = screen.getByRole('region', { name: 'Công thức mới nhất' });
    expect(within(section).getByRole('alert')).toHaveTextContent('Không tải được công thức');
    expect(screen.queryByText('Chưa có công thức nào được đăng tải.')).not.toBeInTheDocument();
    // Lỗi một phần không làm sập trang: danh mục vẫn hiện.
    expect(screen.getByRole('link', { name: /Danh mục 1/ })).toBeInTheDocument();
  });

  it('NFR-USE-003: API danh mục lỗi → báo lỗi ở phần danh mục, công thức vẫn hiện', async () => {
    mockGetCategories.mockRejectedValue(new ApiError(500, 'INTERNAL_ERROR'));

    await renderHome();

    const section = screen.getByRole('region', { name: 'Danh mục món ăn' });
    expect(within(section).getByRole('alert')).toHaveTextContent('Không tải được danh mục');
    expect(screen.getAllByTestId('recipe-card')).toHaveLength(2);
  });

  it('FR-SRCH-001: dùng chung SearchForm — form GET tới /search (chạy cả khi chưa có JS), ô có nhãn', async () => {
    await renderHome();

    const form = screen.getByRole('search');
    expect(form).toHaveAttribute('action', '/search');
    expect(within(form).getByRole('searchbox', { name: 'Từ khóa tìm kiếm' })).toHaveAttribute('name', 'q');
  });

  it('FR-SRCH-001: từ khóa < 2 ký tự → thông báo tiếng Việt, không chuyển trang', async () => {
    await renderHome();

    await userEvent.type(screen.getByRole('searchbox'), ' a ');
    await userEvent.click(screen.getByRole('button', { name: 'Tìm kiếm' }));

    expect(within(screen.getByRole('search')).getByRole('alert')).toHaveTextContent('ít nhất 2 ký tự');
    expect(mockPush).not.toHaveBeenCalled();
  });

  it('FR-SRCH-001: từ khóa hợp lệ → chuyển trang mềm tới /search?q= (đã trim + encode)', async () => {
    await renderHome();

    await userEvent.type(screen.getByRole('searchbox'), '  phở bò ');
    await userEvent.click(screen.getByRole('button', { name: 'Tìm kiếm' }));

    expect(mockPush).toHaveBeenCalledWith(`/search?q=${encodeURIComponent('phở bò')}`);
  });

  it('WCAG: không lồng <main> (layout gốc đã có <main id="main">), có đúng 1 h1', async () => {
    const { container } = await renderHome();

    expect(container.querySelector('main')).toBeNull();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
  });
});
