import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AuthorOverview from '@/app/dashboard/AuthorOverview';
import { apiClient } from '@/lib/api-client';
import type { PagedResult, RecipeSummaryDto } from '@/lib/types';
import { networkError } from '../../utils/problem';
import { renderWithProviders } from '../../utils/render';

jest.mock('@/lib/api-client', () => ({
  ...jest.requireActual('@/lib/api-client'),
  apiClient: { get: jest.fn(), post: jest.fn(), put: jest.fn(), patch: jest.fn(), delete: jest.fn() },
}));
const api = apiClient as jest.Mocked<typeof apiClient>;

function summary(overrides: Partial<RecipeSummaryDto> = {}): RecipeSummaryDto {
  return {
    id: 'r1',
    title: 'Phở bò Hà Nội',
    slug: 'pho-bo-ha-noi',
    description: null,
    featuredImageUrl: null,
    status: 'Published',
    prepTimeMinutes: 30,
    cookTimeMinutes: 480,
    difficulty: 'Medium',
    authorId: 'u1',
    authorName: null,
    createdAt: '2026-09-30T08:00:00Z',
    ...overrides,
  };
}

function paged(items: RecipeSummaryDto[], totalCount = items.length): PagedResult<RecipeSummaryDto> {
  return { items, totalCount, page: 1, pageSize: 5, totalPages: 1, hasNextPage: false, hasPreviousPage: false };
}

/** Trả số đếm theo `status`; không có `status` → danh sách mới nhất. */
function mockOverview(counts: { draft: number; published: number; archived: number }, recent: RecipeSummaryDto[]) {
  api.get.mockImplementation((async (_url: string, config?: { params?: { status?: string } }) => {
    const status = config?.params?.status;
    if (status === 'Draft') return { data: paged([], counts.draft) };
    if (status === 'Published') return { data: paged([], counts.published) };
    if (status === 'Archived') return { data: paged([], counts.archived) };
    return { data: paged(recent, counts.draft + counts.published + counts.archived) };
  }) as never);
}

beforeEach(() => {
  jest.resetAllMocks();
});

describe('AuthorOverview — tổng quan Author ở /dashboard (SRS 5.1, FR-RCP-001/D82)', () => {
  it('NFR-USE-004: đang tải → skeleton', () => {
    api.get.mockReturnValue(new Promise(() => {}) as never);
    renderWithProviders(<AuthorOverview />);

    expect(screen.getByLabelText('Đang tải tổng quan công thức')).toBeInTheDocument();
  });

  it('D82/D81: gọi 4 lần GET /recipes?mine=true — 5 bài mới nhất + đếm từng trạng thái (pageSize=1)', async () => {
    mockOverview({ draft: 0, published: 0, archived: 0 }, []);
    renderWithProviders(<AuthorOverview />);

    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(4));
    expect(api.get).toHaveBeenCalledWith('/recipes', {
      params: { mine: true, page: 1, pageSize: 5, sort: '-createdAt' },
    });
    for (const status of ['Draft', 'Published', 'Archived']) {
      expect(api.get).toHaveBeenCalledWith('/recipes', { params: { mine: true, status, page: 1, pageSize: 1 } });
    }
  });

  it('D82: 4 ô số Tổng / Nháp / Đã xuất bản / Đã lưu trữ, mỗi ô dẫn sang tab tương ứng ở /dashboard/recipes', async () => {
    mockOverview({ draft: 2, published: 5, archived: 1 }, [summary()]);
    renderWithProviders(<AuthorOverview />);

    expect(await screen.findByRole('link', { name: 'Tổng 8' })).toHaveAttribute('href', '/dashboard/recipes');
    expect(screen.getByRole('link', { name: 'Nháp 2' })).toHaveAttribute('href', '/dashboard/recipes?status=draft');
    expect(screen.getByRole('link', { name: 'Đã xuất bản 5' })).toHaveAttribute(
      'href',
      '/dashboard/recipes?status=published',
    );
    expect(screen.getByRole('link', { name: 'Đã lưu trữ 1' })).toHaveAttribute(
      'href',
      '/dashboard/recipes?status=archived',
    );
  });

  it('D82: danh sách gần đây — tiêu đề dẫn tới trang sửa, có badge trạng thái, có link "Xem tất cả"', async () => {
    mockOverview({ draft: 1, published: 1, archived: 0 }, [
      summary({ id: 'r2', title: 'Gỏi cuốn tôm thịt', slug: 'goi-cuon', status: 'Draft' }),
      summary({ id: 'r1', title: 'Phở bò Hà Nội', status: 'Published' }),
    ]);
    renderWithProviders(<AuthorOverview />);

    const list = await screen.findByRole('list', { name: 'Công thức gần đây' });
    const items = within(list).getAllByRole('listitem');
    expect(items).toHaveLength(2);
    expect(within(items[0]).getByRole('link', { name: 'Gỏi cuốn tôm thịt' })).toHaveAttribute(
      'href',
      '/dashboard/recipes/r2/edit',
    );
    expect(within(items[0]).getByText('Nháp')).toBeInTheDocument();
    expect(within(items[1]).getByText('Đã xuất bản')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Xem tất cả công thức của tôi' })).toHaveAttribute(
      'href',
      '/dashboard/recipes',
    );
  });

  it('D82: chưa có công thức nào → lời mời tạo công thức, không có danh sách gần đây', async () => {
    mockOverview({ draft: 0, published: 0, archived: 0 }, []);
    renderWithProviders(<AuthorOverview />);

    expect(await screen.findByText('Bạn chưa có công thức nào.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Tạo công thức đầu tiên' })).toHaveAttribute(
      'href',
      '/dashboard/recipes/new',
    );
    expect(screen.queryByRole('list', { name: 'Công thức gần đây' })).not.toBeInTheDocument();
  });

  it('D82: một lời gọi lỗi → báo lỗi + nút "Thử lại"; bấm thử lại tải được thì hiện số liệu', async () => {
    api.get.mockRejectedValue(networkError());
    renderWithProviders(<AuthorOverview />);

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('Không tải được tổng quan công thức')).toBeInTheDocument();

    mockOverview({ draft: 1, published: 0, archived: 0 }, [summary({ status: 'Draft' })]);
    await userEvent.click(within(alert).getByRole('button', { name: 'Thử lại' }));

    expect(await screen.findByRole('link', { name: 'Nháp 1' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
