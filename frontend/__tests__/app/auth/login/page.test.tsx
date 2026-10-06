import { screen } from '@testing-library/react';
import { redirect } from 'next/navigation';
import LoginPage, { metadata } from '@/app/auth/login/page';
import { auth } from '@/auth';
import { renderWithProviders } from '../../../utils/render';

jest.mock('@/auth', () => ({ auth: jest.fn() }));
jest.mock('@/app/auth/login/actions', () => ({ loginAction: jest.fn() }));
jest.mock('next-auth/react', () => ({ useSession: () => ({ update: jest.fn() }) }));

const mockAuth = auth as unknown as jest.Mock;
const mockRedirect = redirect as unknown as jest.Mock;

const params = (value: Record<string, string | string[]> = {}) => ({
  searchParams: Promise.resolve(value),
});

beforeEach(() => {
  mockAuth.mockReset();
  mockRedirect.mockReset();
});

describe('/auth/login — FR-AUTH-002', () => {
  it('FR-AUTH-002: metadata có title "Đăng nhập" và noindex', () => {
    expect(metadata.title).toBe('Đăng nhập');
    expect(metadata.robots).toMatchObject({ index: false });
  });

  it('FR-AUTH-002: chưa đăng nhập → có h1, form, link đăng ký và link về trang chủ', async () => {
    mockAuth.mockResolvedValue(null);
    renderWithProviders(await LoginPage(params()));

    expect(
      screen.getByRole('heading', { level: 1, name: 'Chào mừng trở lại' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Đăng nhập' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Đăng ký' })).toHaveAttribute('href', '/auth/register');
    expect(screen.getByRole('link', { name: /về trang chủ/ })).toHaveAttribute('href', '/');
    expect(mockRedirect).not.toHaveBeenCalled();
  });

  it('FR-AUTH-002: đã đăng nhập → chuyển sang callbackUrl nội bộ', async () => {
    mockAuth.mockResolvedValue({ expires: '2026-09-30T00:00:00Z' });
    mockRedirect.mockImplementation(() => {
      throw new Error('NEXT_REDIRECT');
    });

    await expect(LoginPage(params({ callbackUrl: '/dashboard/recipes' }))).rejects.toThrow(
      'NEXT_REDIRECT',
    );
    expect(mockRedirect).toHaveBeenCalledWith('/dashboard/recipes');
  });

  it('FR-AUTH-002: callbackUrl ra ngoài → bỏ qua, dùng /dashboard (chống open redirect)', async () => {
    mockAuth.mockResolvedValue({ expires: '2026-09-30T00:00:00Z' });
    mockRedirect.mockImplementation(() => {
      throw new Error('NEXT_REDIRECT');
    });

    await expect(LoginPage(params({ callbackUrl: '//evil.com' }))).rejects.toThrow('NEXT_REDIRECT');
    expect(mockRedirect).toHaveBeenCalledWith('/dashboard');
  });

  it('FR-AUTH-003/D33: có ?error= (Auth.js redirect sau khi Google profile() lỗi) → LoginForm hiện toast lỗi chung', async () => {
    mockAuth.mockResolvedValue(null);
    renderWithProviders(await LoginPage(params({ error: 'OAuthCallbackError' })));

    expect(await screen.findByRole('alert')).toHaveTextContent('Đăng nhập Google thất bại');
  });

  it('FR-AUTH-003: không có ?error= → không có toast lỗi Google nào', async () => {
    mockAuth.mockResolvedValue(null);
    renderWithProviders(await LoginPage(params()));

    expect(screen.queryByText('Đăng nhập Google thất bại')).not.toBeInTheDocument();
  });
});

describe('/auth/login — FR-AUTH-004', () => {
  it('FR-AUTH-004: ?error=SessionExpired (refresh token hết hạn/bị thu hồi) → toast "phiên hết hạn", KHÔNG phải lỗi Google', async () => {
    mockAuth.mockResolvedValue(null);
    renderWithProviders(await LoginPage(params({ error: 'SessionExpired' })));

    expect(await screen.findByRole('status')).toHaveTextContent('Phiên đăng nhập đã hết hạn');
    expect(screen.queryByText('Đăng nhập Google thất bại')).not.toBeInTheDocument();
  });
});
