import { render, screen, within } from '@testing-library/react';
import HomePage, { revalidate } from '@/app/(home)/page';
import { ApiError, getCategories } from '@/lib/categories';
import { getLatestRecipes } from '@/lib/recipes';
import type { PagedResult, RecipeSummaryDto } from '@/lib/types';

jest.mock('@/lib/categories', () => ({
  ...jest.requireActual('@/lib/categories'),
  getCategories: jest.fn(),
}));
jest.mock('@/lib/recipes', () => ({
  ...jest.requireActual('@/lib/recipes'),
  getLatestRecipes: jest.fn(),
}));

const mockGetCategories = getCategories as jest.Mock;
const mockGetLatestRecipes = getLatestRecipes as jest.Mock;

const recipe = (i: number, overrides: Partial<RecipeSummaryDto> = {}): RecipeSummaryDto => ({
  id: `r${i}`,
  title: `Món ${i}`,
  slug: `mon-${i}`,
  description: `Mô tả món ${i}`,
  featuredImageUrl: null,
  status: 'Published',
  prepTimeMinutes: 5,
  cookTimeMinutes: 10,
  difficulty: 'Easy',
  authorId: 'u1',
  authorName: 'Bếp Nhà',
  createdAt: '2026-10-01T00:00:00Z',
  ...overrides,
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
  mockGetLatestRecipes.mockResolvedValue(paged([1, 2, 3].map((i) => recipe(i))));
});

describe('/ — trang chủ dạng thực đơn (FR-RCP-001, D97)', () => {
  it('SRS 5.1: trang chủ ISR revalidate 3600', () => {
    expect(revalidate).toBe(3600);
  });

  it('D97: đúng 1 h1 "Hôm nay nấu gì đây?", không lồng <main>', async () => {
    const { container } = await renderHome();

    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Hôm nay nấu gì đây?');
    expect(container.querySelector('main')).toBeNull();
  });

  it('FR-RCP-001: lấy 6 công thức mới nhất; món đầu là "Món đáng thử" có link tới trang chi tiết', async () => {
    await renderHome();

    expect(mockGetLatestRecipes).toHaveBeenCalledWith(6);
    const featured = screen.getByRole('region', { name: 'Món đáng thử' });
    expect(within(featured).getByRole('heading', { level: 2, name: 'Món 1' })).toBeInTheDocument();
    expect(within(featured).getByText('Mô tả món 1')).toBeInTheDocument();
    expect(within(featured).getByRole('link', { name: 'Xem công thức Món 1' })).toHaveAttribute('href', '/recipes/mon-1');
  });

  it('D97: các món còn lại thành "Thực đơn mới" — đánh số 01, 02…, tổng thời gian, độ khó, link chi tiết', async () => {
    await renderHome();

    const menu = screen.getByRole('region', { name: 'Thực đơn mới' });
    const rows = within(menu).getAllByRole('listitem');
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveTextContent('01');
    expect(rows[0]).toHaveTextContent('15 phút');
    expect(rows[0]).toHaveTextContent('Dễ');
    expect(within(rows[0]).getByRole('link', { name: 'Món 2' })).toHaveAttribute('href', '/recipes/mon-2');
    expect(rows[1]).toHaveTextContent('02');
    expect(within(menu).getByRole('link', { name: 'Xem cả thực đơn' })).toHaveAttribute('href', '/recipes');
  });

  it('D97: thời gian ≥ 60 phút hiện theo giờ ("3 giờ 40 phút", "1 giờ")', async () => {
    mockGetLatestRecipes.mockResolvedValue(
      paged([
        recipe(1),
        recipe(2, { prepTimeMinutes: 40, cookTimeMinutes: 180 }),
        recipe(3, { prepTimeMinutes: 30, cookTimeMinutes: 30 }),
      ]),
    );

    await renderHome();

    const rows = within(screen.getByRole('region', { name: 'Thực đơn mới' })).getAllByRole('listitem');
    expect(rows[0]).toHaveTextContent('3 giờ 40 phút');
    expect(rows[1]).toHaveTextContent('1 giờ');
    expect(rows[1]).not.toHaveTextContent('1 giờ 0 phút');
  });

  it('D97: danh mục nằm ở thanh bên (layout), trang chủ không gọi lại API danh mục', async () => {
    await renderHome();

    expect(mockGetCategories).not.toHaveBeenCalled();
  });

  it('chỉ có 1 công thức → hiện Món đáng thử, không có Thực đơn mới rỗng', async () => {
    mockGetLatestRecipes.mockResolvedValue(paged([recipe(1)]));

    await renderHome();

    expect(screen.getByRole('region', { name: 'Món đáng thử' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Thực đơn mới' })).not.toBeInTheDocument();
  });

  it('chưa có công thức → thông báo trống (không phải lỗi)', async () => {
    mockGetLatestRecipes.mockResolvedValue(paged([]));

    await renderHome();

    expect(screen.getByText('Chưa có công thức nào được đăng tải.')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Món đáng thử' })).not.toBeInTheDocument();
  });

  it('NFR-USE-003: API công thức lỗi → báo lỗi rõ ràng, không giả làm "chưa có công thức"', async () => {
    mockGetLatestRecipes.mockRejectedValue(new ApiError(500, 'INTERNAL_ERROR'));

    await renderHome();

    expect(screen.getByRole('alert')).toHaveTextContent('Không tải được công thức');
    expect(screen.queryByText('Chưa có công thức nào được đăng tải.')).not.toBeInTheDocument();
    // Lỗi không làm sập trang: tiêu đề vẫn hiện.
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument();
  });
});
