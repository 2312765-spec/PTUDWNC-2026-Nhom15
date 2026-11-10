import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SiteSidebar } from '@/components/layout/SiteSidebar';
import { ApiError, getCategories } from '@/lib/categories';
import type { CategoryDto } from '@/lib/types';
import { mockPush } from '../../../jest.setup';

jest.mock('@/lib/categories', () => ({
  ...jest.requireActual('@/lib/categories'),
  getCategories: jest.fn(),
}));
// HeaderAuth đọc phiên (useSession) — đã có test riêng; ở đây chỉ cần biết nó được gắn vào thanh bên.
jest.mock('@/components/auth/HeaderAuth', () => ({
  HeaderAuth: () => <div data-testid="header-auth" />,
}));

const mockGetCategories = getCategories as jest.Mock;

const category = (i: number): CategoryDto => ({
  id: `c${i}`,
  name: `Danh mục ${i}`,
  slug: `danh-muc-${i}`,
  description: null,
  recipeCount: i,
});

async function renderSidebar() {
  return render(await SiteSidebar());
}

beforeEach(() => {
  jest.resetAllMocks();
  mockGetCategories.mockResolvedValue([category(1), category(2)]);
});

describe('SiteSidebar — thanh bên mục lục (D97)', () => {
  it('NFR-USE-002: là landmark banner, logo về trang chủ, điều hướng chính giữ nguyên chữ link', async () => {
    await renderSidebar();

    expect(screen.getByRole('banner')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Culinary Blog' })).toHaveAttribute('href', '/');
    const nav = screen.getByRole('navigation', { name: 'Điều hướng chính' });
    expect(within(nav).getByRole('link', { name: 'Công thức' })).toHaveAttribute('href', '/recipes');
    expect(within(nav).getByRole('link', { name: 'Danh mục' })).toHaveAttribute('href', '/categories');
    expect(within(nav).getByRole('link', { name: 'Tìm kiếm' })).toHaveAttribute('href', '/search');
    expect(screen.getByTestId('header-auth')).toBeInTheDocument();
  });

  it('FR-CAT-001: Mục lục đánh số 01, 02… mỗi danh mục link tới /categories/{slug}', async () => {
    await renderSidebar();

    // Hai bản Mục lục (cột trái ≥ lg / <details> màn nhỏ) — CSS chỉ hiện một; nội dung phải giống nhau.
    const tocs = screen.getAllByRole('navigation', { name: 'Mục lục', hidden: true });
    expect(tocs).toHaveLength(2);
    for (const toc of tocs) {
      const links = within(toc).getAllByRole('link', { hidden: true });
      expect(links.map((a) => a.getAttribute('href'))).toEqual(['/categories/danh-muc-1', '/categories/danh-muc-2']);
      expect(links[0]).toHaveTextContent('01');
      expect(links[0]).toHaveTextContent('Danh mục 1');
    }
  });

  it('NFR-USE-003: API danh mục lỗi → ẩn Mục lục, thanh bên vẫn hiện điều hướng', async () => {
    mockGetCategories.mockRejectedValue(new ApiError(500, 'INTERNAL_ERROR'));

    await renderSidebar();

    expect(screen.queryByRole('navigation', { name: 'Mục lục', hidden: true })).not.toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Điều hướng chính' })).toBeInTheDocument();
  });

  it('FR-SRCH-001: ô tìm kiếm chung — form GET tới /search (chạy cả khi chưa có JS), ô có nhãn', async () => {
    await renderSidebar();

    const form = screen.getByRole('search');
    expect(form).toHaveAttribute('action', '/search');
    expect(within(form).getByRole('searchbox', { name: 'Từ khóa tìm kiếm' })).toHaveAttribute('name', 'q');
  });

  it('FR-SRCH-001: từ khóa < 2 ký tự → thông báo tiếng Việt, không chuyển trang', async () => {
    await renderSidebar();

    await userEvent.type(screen.getByRole('searchbox'), ' a ');
    await userEvent.click(screen.getByRole('button', { name: 'Tìm kiếm' }));

    expect(within(screen.getByRole('search')).getByRole('alert')).toHaveTextContent('ít nhất 2 ký tự');
    expect(mockPush).not.toHaveBeenCalled();
  });

  it('FR-SRCH-001: từ khóa hợp lệ → chuyển trang mềm tới /search?q= (đã trim + encode)', async () => {
    await renderSidebar();

    await userEvent.type(screen.getByRole('searchbox'), '  phở bò ');
    await userEvent.click(screen.getByRole('button', { name: 'Tìm kiếm' }));

    expect(mockPush).toHaveBeenCalledWith(`/search?q=${encodeURIComponent('phở bò')}`);
  });
});
