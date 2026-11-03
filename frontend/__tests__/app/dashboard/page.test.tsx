import { render, screen } from '@testing-library/react';
import { useSession } from 'next-auth/react';
import DashboardPage from '@/app/dashboard/page';
import type { UserProfile } from '@/lib/types';

jest.mock('next-auth/react', () => ({ useSession: jest.fn() }));
const mockUseSession = useSession as jest.Mock;

const mockReplace = jest.fn();
jest.mock('next/navigation', () => ({
  useRouter: () => ({ push: jest.fn(), replace: mockReplace, back: jest.fn(), prefetch: jest.fn() }),
  usePathname: () => '/dashboard',
}));

const author: UserProfile = {
  id: 'u1',
  email: 'lan@example.com',
  displayName: 'Bếp của Lan',
  avatarUrl: null,
  bio: null,
  roles: ['Author'],
};
const admin: UserProfile = { ...author, id: 'u2', roles: ['Author', 'Admin'] };

function signedInAs(profile: UserProfile) {
  mockUseSession.mockReturnValue({ status: 'authenticated', data: { profile, expires: '' } });
}

beforeEach(() => {
  jest.resetAllMocks();
});

describe('DashboardPage — trang đích của link trong email chào mừng (FR-JOB-001/D12)', () => {
  it('NFR-USE-004: đang tải phiên → skeleton, chưa hiện lời chào', () => {
    mockUseSession.mockReturnValue({ status: 'loading', data: null });

    render(<DashboardPage />);

    expect(screen.getByLabelText('Đang kiểm tra phiên đăng nhập')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
    expect(mockReplace).not.toHaveBeenCalled();
  });

  it('permissions.md: chưa đăng nhập → chuyển tới /auth/login kèm callbackUrl=/dashboard', () => {
    mockUseSession.mockReturnValue({ status: 'unauthenticated', data: null });

    render(<DashboardPage />);

    expect(mockReplace).toHaveBeenCalledWith('/auth/login?callbackUrl=%2Fdashboard');
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
  });

  it('D5: đã đăng nhập → chào bằng displayName, có lối tắt tới hồ sơ và danh sách công thức', () => {
    signedInAs(author);

    render(<DashboardPage />);

    expect(screen.getByRole('heading', { level: 1, name: 'Chào mừng, Bếp của Lan!' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Hồ sơ của tôi/ })).toHaveAttribute('href', '/dashboard/profile');
    expect(screen.getByRole('link', { name: /Khám phá công thức/ })).toHaveAttribute('href', '/recipes');
    expect(mockReplace).not.toHaveBeenCalled();
  });

  it('permissions.md: Author không thấy lối tắt quản lý danh mục', () => {
    signedInAs(author);

    render(<DashboardPage />);

    expect(screen.queryByRole('link', { name: /Quản lý danh mục/ })).not.toBeInTheDocument();
  });

  it('permissions.md: Admin thấy lối tắt quản lý danh mục', () => {
    signedInAs(admin);

    render(<DashboardPage />);

    expect(screen.getByRole('link', { name: /Quản lý danh mục/ })).toHaveAttribute(
      'href',
      '/dashboard/categories',
    );
  });

  it('displayName chứa HTML → hiện như văn bản, không tạo phần tử script', () => {
    signedInAs({ ...author, displayName: '<script>alert(1)</script>' });

    const { container } = render(<DashboardPage />);

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('<script>alert(1)</script>');
    expect(container.querySelector('script')).toBeNull();
  });
});
