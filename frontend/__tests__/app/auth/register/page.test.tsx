import { screen } from '@testing-library/react';
import { redirect } from 'next/navigation';
import RegisterPage, { metadata } from '@/app/auth/register/page';
import { auth } from '@/auth';
import { renderWithProviders } from '../../../utils/render';

jest.mock('@/auth', () => ({ auth: jest.fn() }));
jest.mock('@/app/auth/register/actions', () => ({ registerAction: jest.fn() }));
jest.mock('next-auth/react', () => ({ useSession: () => ({ update: jest.fn() }) }));

const mockAuth = auth as unknown as jest.Mock;
const mockRedirect = redirect as unknown as jest.Mock;

beforeEach(() => {
  mockAuth.mockReset();
  mockRedirect.mockClear();
});

describe('/auth/register — FR-AUTH-001', () => {
  it('FR-AUTH-001: metadata có title "Đăng ký" và noindex', () => {
    expect(metadata.title).toBe('Đăng ký');
    expect(metadata.robots).toMatchObject({ index: false });
  });

  it('FR-AUTH-001: chưa đăng nhập → có h1, link đăng nhập và link về trang chủ', async () => {
    mockAuth.mockResolvedValue(null);
    renderWithProviders(await RegisterPage());

    expect(screen.getByRole('heading', { level: 1, name: 'Tạo tài khoản mới' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Đăng nhập' })).toHaveAttribute('href', '/auth/login');
    expect(screen.getByRole('link', { name: /về trang chủ/ })).toHaveAttribute('href', '/');
    expect(mockRedirect).not.toHaveBeenCalled();
  });

  it('FR-AUTH-001 (tiên quyết 1): đã đăng nhập → chuyển sang /dashboard', async () => {
    mockAuth.mockResolvedValue({ expires: '2026-09-30T00:00:00Z' });
    mockRedirect.mockImplementation(() => {
      throw new Error('NEXT_REDIRECT');
    });

    await expect(RegisterPage()).rejects.toThrow('NEXT_REDIRECT');
    expect(mockRedirect).toHaveBeenCalledWith('/dashboard');
  });
});
