import { render, screen } from '@testing-library/react';
import { useSession } from 'next-auth/react';
import DashboardRecipesLayout from '@/app/dashboard/recipes/layout';

jest.mock('next-auth/react', () => ({ useSession: jest.fn() }));
const mockUseSession = useSession as jest.Mock;

const mockReplace = jest.fn();
jest.mock('next/navigation', () => ({
  useRouter: () => ({ push: jest.fn(), replace: mockReplace, back: jest.fn(), prefetch: jest.fn() }),
  usePathname: () => '/dashboard/recipes/new',
}));

function profile(roles: string[]) {
  return { status: 'authenticated', data: { profile: { id: 'u1', email: 'a@x.vn', displayName: 'A', avatarUrl: null, bio: null, roles } } };
}

function renderLayout() {
  render(
    <DashboardRecipesLayout>
      <p>Trình soạn công thức</p>
    </DashboardRecipesLayout>,
  );
}

beforeEach(() => {
  jest.resetAllMocks();
});

describe('DashboardRecipesLayout — chặn quyền /dashboard/recipes/* (permissions.md mục 4)', () => {
  it('đang tải phiên → skeleton', () => {
    mockUseSession.mockReturnValue({ status: 'loading', data: null });
    renderLayout();

    expect(screen.getByLabelText('Đang kiểm tra quyền truy cập')).toBeInTheDocument();
    expect(screen.queryByText('Trình soạn công thức')).not.toBeInTheDocument();
  });

  it('chưa đăng nhập → /auth/login kèm callbackUrl là trang hiện tại', () => {
    mockUseSession.mockReturnValue({ status: 'unauthenticated', data: null });
    renderLayout();

    expect(mockReplace).toHaveBeenCalledWith('/auth/login?callbackUrl=%2Fdashboard%2Frecipes%2Fnew');
  });

  it.each([[['Author']], [['Admin']], [['Author', 'Admin']]])('roles %j → hiện nội dung', (roles) => {
    mockUseSession.mockReturnValue(profile(roles));
    renderLayout();

    expect(screen.getByText('Trình soạn công thức')).toBeInTheDocument();
  });

  it('không có role Author/Admin → 403', () => {
    mockUseSession.mockReturnValue(profile([]));
    renderLayout();

    expect(screen.getByRole('heading', { name: 'Truy cập bị từ chối (403)' })).toBeInTheDocument();
    expect(screen.queryByText('Trình soạn công thức')).not.toBeInTheDocument();
  });
});
