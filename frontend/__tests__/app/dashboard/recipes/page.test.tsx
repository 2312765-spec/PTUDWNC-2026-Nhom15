import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import MyRecipesPage from '@/app/dashboard/recipes/page';
import { apiClient } from '@/lib/api-client';
import type { PagedResult, RecipeSummaryDto } from '@/lib/types';
import { networkError } from '../../../utils/problem';
import { renderWithProviders } from '../../../utils/render';

const replace = jest.fn();
let searchParams = new URLSearchParams();
jest.mock('next/navigation', () => ({
  useRouter: () => ({ push: jest.fn(), replace, back: jest.fn(), prefetch: jest.fn() }),
  usePathname: () => '/dashboard/recipes',
  useSearchParams: () => searchParams,
}));

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

function paged(items: RecipeSummaryDto[], overrides: Partial<PagedResult<RecipeSummaryDto>> = {}): PagedResult<RecipeSummaryDto> {
  return {
    items,
    totalCount: items.length,
    page: 1,
    pageSize: 10,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
    ...overrides,
  };
}

beforeEach(() => {
  jest.resetAllMocks();
  searchParams = new URLSearchParams();
});

describe('/dashboard/recipes — công thức của tôi (FR-RCP-001/D81)', () => {
  it('NFR-USE-004: đang tải → skeleton', () => {
    api.get.mockReturnValue(new Promise(() => {}) as never);
    renderWithProviders(<MyRecipesPage />);

    expect(screen.getByLabelText('Đang tải danh sách công thức')).toBeInTheDocument();
  });

  it('D81: gọi GET /recipes?mine=true, hiện tiêu đề, trạng thái, link Sửa; link Xem chỉ ở bài Published', async () => {
    api.get.mockResolvedValue({
      data: paged([
        summary({ id: 'r1', title: 'Phở bò Hà Nội', slug: 'pho-bo-ha-noi', status: 'Published' }),
        summary({ id: 'r2', title: 'Gỏi cuốn', slug: 'goi-cuon', status: 'Draft' }),
        summary({ id: 'r3', title: 'Canh chua', slug: 'canh-chua', status: 'Archived' }),
      ]),
    } as never);
    renderWithProviders(<MyRecipesPage />);

    const list = await screen.findByRole('list', { name: 'Công thức của tôi' });
    expect(api.get).toHaveBeenCalledWith('/recipes', { params: { mine: true, page: 1, pageSize: 10 } });
    const items = within(list).getAllByRole('listitem');
    expect(items).toHaveLength(3);

    expect(within(items[0]).getByText('Đã xuất bản')).toBeInTheDocument();
    expect(within(items[0]).getByRole('link', { name: 'Sửa Phở bò Hà Nội' })).toHaveAttribute('href', '/dashboard/recipes/r1/edit');
    expect(within(items[0]).getByRole('link', { name: 'Xem Phở bò Hà Nội' })).toHaveAttribute('href', '/recipes/pho-bo-ha-noi');

    expect(within(items[1]).getByText('Nháp')).toBeInTheDocument();
    expect(within(items[1]).queryByRole('link', { name: /^Xem/ })).not.toBeInTheDocument();
    expect(within(items[2]).getByText('Đã lưu trữ')).toBeInTheDocument();
    expect(within(items[2]).queryByRole('link', { name: /^Xem/ })).not.toBeInTheDocument();
    expect(screen.getByText('3 công thức')).toBeInTheDocument();
  });

  it('D81: URL ?status=draft&page=2 → tab Nháp được chọn, gọi API với status=Draft, page=2', async () => {
    searchParams = new URLSearchParams('status=draft&page=2');
    api.get.mockResolvedValue({ data: paged([summary({ status: 'Draft' })], { page: 2 }) } as never);
    renderWithProviders(<MyRecipesPage />);

    await screen.findByRole('list', { name: 'Công thức của tôi' });
    expect(api.get).toHaveBeenCalledWith('/recipes', { params: { mine: true, status: 'Draft', page: 2, pageSize: 10 } });
    expect(screen.getByRole('tab', { name: 'Nháp' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'Tất cả' })).toHaveAttribute('aria-selected', 'false');
  });

  it('D81: bấm tab Đã lưu trữ → đổi URL sang ?status=archived (về trang 1)', async () => {
    searchParams = new URLSearchParams('page=3');
    api.get.mockResolvedValue({ data: paged([summary()]) } as never);
    const user = userEvent.setup();
    renderWithProviders(<MyRecipesPage />);
    await screen.findByRole('list', { name: 'Công thức của tôi' });

    await user.click(screen.getByRole('tab', { name: 'Đã lưu trữ' }));

    expect(replace).toHaveBeenCalledWith('/dashboard/recipes?status=archived');
  });

  it('WCAG 2.1: phím mũi tên phải chuyển sang tab kế tiếp', async () => {
    api.get.mockResolvedValue({ data: paged([summary()]) } as never);
    const user = userEvent.setup();
    renderWithProviders(<MyRecipesPage />);
    await screen.findByRole('list', { name: 'Công thức của tôi' });

    screen.getByRole('tab', { name: 'Tất cả' }).focus();
    await user.keyboard('{ArrowRight}');

    expect(replace).toHaveBeenCalledWith('/dashboard/recipes?status=draft');
  });

  it('FR-SRCH-004: nhiều trang → bấm trang 2 đổi URL, giữ status', async () => {
    searchParams = new URLSearchParams('status=published');
    api.get.mockResolvedValue({
      data: paged([summary()], { totalCount: 25, totalPages: 3, hasNextPage: true }),
    } as never);
    const user = userEvent.setup();
    renderWithProviders(<MyRecipesPage />);
    await screen.findByRole('list', { name: 'Công thức của tôi' });

    await user.click(screen.getByRole('button', { name: '2' }));

    expect(replace).toHaveBeenCalledWith('/dashboard/recipes?status=published&page=2');
  });

  it('Trạng thái rỗng → lời nhắc + link Tạo công thức mới', async () => {
    api.get.mockResolvedValue({ data: paged([]) } as never);
    renderWithProviders(<MyRecipesPage />);

    expect(await screen.findByText('Bạn chưa có công thức nào.')).toBeInTheDocument();
    const links = screen.getAllByRole('link', { name: /Tạo công thức mới/ });
    links.forEach((link) => expect(link).toHaveAttribute('href', '/dashboard/recipes/new'));
  });

  it('Tab Nháp rỗng → lời nhắc theo tab', async () => {
    searchParams = new URLSearchParams('status=draft');
    api.get.mockResolvedValue({ data: paged([]) } as never);
    renderWithProviders(<MyRecipesPage />);

    expect(await screen.findByText('Không có công thức nào ở trạng thái Nháp.')).toBeInTheDocument();
  });

  it('NFR-USE-003: API lỗi → thông báo + Thử lại gọi lại API', async () => {
    api.get.mockRejectedValueOnce(networkError());
    api.get.mockResolvedValueOnce({ data: paged([summary()]) } as never);
    const user = userEvent.setup();
    renderWithProviders(<MyRecipesPage />);

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('Không tải được danh sách công thức')).toBeInTheDocument();
    await user.click(within(alert).getByRole('button', { name: 'Thử lại' }));

    expect(await screen.findByRole('list', { name: 'Công thức của tôi' })).toBeInTheDocument();
    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(2));
  });

  it('URL status lạ → coi như Tất cả (không gửi status lên API)', async () => {
    searchParams = new URLSearchParams('status=deleted');
    api.get.mockResolvedValue({ data: paged([summary()]) } as never);
    renderWithProviders(<MyRecipesPage />);

    await screen.findByRole('list', { name: 'Công thức của tôi' });
    expect(api.get).toHaveBeenCalledWith('/recipes', { params: { mine: true, page: 1, pageSize: 10 } });
    expect(screen.getByRole('tab', { name: 'Tất cả' })).toHaveAttribute('aria-selected', 'true');
  });
});
