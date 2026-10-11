import { render, screen } from '@testing-library/react';
import { useSession } from 'next-auth/react';
import AdminCategoriesLayout from '@/app/dashboard/categories/layout';
import type { UserProfile } from '@/lib/types';

jest.mock('next-auth/react', () => ({ useSession: jest.fn() }));
const mockUseSession = useSession as jest.Mock;

const mockReplace = jest.fn();
jest.mock('next/navigation', () => ({
  useRouter: () => ({ push: jest.fn(), replace: mockReplace, back: jest.fn(), prefetch: jest.fn() }),
  usePathname: () => '/dashboard/categories',
}));

const author: UserProfile = {
  id: 'u1',
  email: 'author@example.com',
  displayName: 'Tác giả',
  avatarUrl: null,
  bio: null,
  roles: ['Author'],
};
const admin: UserProfile = { ...author, id: 'u2', email: 'admin@example.com', roles: ['Author', 'Admin'] };

function renderLayout() {
  render(
    <AdminCategoriesLayout>
      <p>Nội dung quản trị</p>
    </AdminCategoriesLayout>,
  );
}

beforeEach(() => {
  jest.resetAllMocks();
  localStorage.clear();
  sessionStorage.clear();
});

describe('AdminCategoriesLayout — chặn quyền Admin cho /dashboard/categories (FR-CAT-001/003/004/005)', () => {
  it('NFR-USE-004: đang tải phiên → skeleton, chưa hiện nội dung', () => {
    mockUseSession.mockReturnValue({ status: 'loading', data: null });

    renderLayout();

    expect(screen.getByLabelText('Đang kiểm tra quyền truy cập')).toBeInTheDocument();
    expect(screen.queryByText('Nội dung quản trị')).not.toBeInTheDocument();
    expect(mockReplace).not.toHaveBeenCalled();
  });

  it('permissions.md: chưa đăng nhập → chuyển tới /auth/login kèm callbackUrl', () => {
    mockUseSession.mockReturnValue({ status: 'unauthenticated', data: null });

    renderLayout();

    expect(mockReplace).toHaveBeenCalledWith('/auth/login?callbackUrl=%2Fdashboard%2Fcategories');
    expect(screen.queryByText('Nội dung quản trị')).not.toBeInTheDocument();
  });

  it('NFR-SEC-006: Author (không phải Admin) → 403, không hiện nội dung', () => {
    mockUseSession.mockReturnValue({ status: 'authenticated', data: { profile: author, expires: '' } });

    renderLayout();

    expect(screen.getByRole('alert')).toHaveTextContent('Truy cập bị từ chối (403)');
    expect(screen.getByRole('link', { name: 'Trở về Trang chủ' })).toHaveAttribute('href', '/');
    expect(screen.queryByText('Nội dung quản trị')).not.toBeInTheDocument();
    expect(mockReplace).not.toHaveBeenCalled();
  });

  it('Admin đã đăng nhập (role lấy từ session Auth.js) → hiện nội dung, không redirect', () => {
    mockUseSession.mockReturnValue({ status: 'authenticated', data: { profile: admin, expires: '' } });

    renderLayout();

    expect(screen.getByText('Nội dung quản trị')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(mockReplace).not.toHaveBeenCalled();
  });

  it('phiên thiếu profile → 403 (mặc định chặn, không mặc định cho qua)', () => {
    mockUseSession.mockReturnValue({ status: 'authenticated', data: { expires: '' } });

    renderLayout();

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.queryByText('Nội dung quản trị')).not.toBeInTheDocument();
  });

  it('không tin localStorage: có "user" Admin giả trong localStorage nhưng phiên là Author → 403', () => {
    localStorage.setItem('user', JSON.stringify({ roles: ['Admin'] }));
    localStorage.setItem('token', 'gia-mao');
    mockUseSession.mockReturnValue({ status: 'authenticated', data: { profile: author, expires: '' } });

    renderLayout();

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.queryByText('Nội dung quản trị')).not.toBeInTheDocument();
  });
});
