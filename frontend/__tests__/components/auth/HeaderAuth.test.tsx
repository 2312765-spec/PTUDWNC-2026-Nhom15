import { render, screen } from '@testing-library/react';
import { useSession } from 'next-auth/react';
import { HeaderAuth } from '@/components/auth/HeaderAuth';

jest.mock('next-auth/react', () => ({ useSession: jest.fn(), signOut: jest.fn() }));
const mockUseSession = useSession as jest.Mock;

const profile = {
  id: 'u1',
  email: 'vinh@example.com',
  displayName: 'Vinh',
  avatarUrl: null,
  bio: null,
  roles: ['Author'],
};

beforeEach(() => {
  jest.resetAllMocks();
});

describe('HeaderAuth — FR-AUTH-005', () => {
  it('FR-AUTH-005: đã đăng nhập → hiện tên và nút Đăng xuất', () => {
    mockUseSession.mockReturnValue({ status: 'authenticated', data: { profile, expires: '' } });

    render(<HeaderAuth />);

    expect(screen.getByText('Vinh')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Đăng xuất' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Đăng nhập' })).not.toBeInTheDocument();
  });

  it('FR-AUTH-005: chưa đăng nhập → link Đăng nhập/Đăng ký, không có nút Đăng xuất', () => {
    mockUseSession.mockReturnValue({ status: 'unauthenticated', data: null });

    render(<HeaderAuth />);

    expect(screen.getByRole('link', { name: 'Đăng nhập' })).toHaveAttribute('href', '/auth/login');
    expect(screen.getByRole('link', { name: 'Đăng ký' })).toHaveAttribute('href', '/auth/register');
    expect(screen.queryByRole('button', { name: 'Đăng xuất' })).not.toBeInTheDocument();
  });

  it('FR-AUTH-004/005: phiên đã chết (session.error) → coi như chưa đăng nhập', () => {
    mockUseSession.mockReturnValue({
      status: 'authenticated',
      data: { profile, error: 'RefreshTokenError', expires: '' },
    });

    render(<HeaderAuth />);

    expect(screen.getByRole('link', { name: 'Đăng nhập' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Đăng xuất' })).not.toBeInTheDocument();
  });

  it('NFR-USE-004: đang tải phiên → skeleton, không nháy link Đăng nhập', () => {
    mockUseSession.mockReturnValue({ status: 'loading', data: null });

    render(<HeaderAuth />);

    expect(screen.getByLabelText('Đang tải phiên đăng nhập')).toHaveAttribute('aria-busy', 'true');
    expect(screen.queryByRole('link', { name: 'Đăng nhập' })).not.toBeInTheDocument();
  });
});
