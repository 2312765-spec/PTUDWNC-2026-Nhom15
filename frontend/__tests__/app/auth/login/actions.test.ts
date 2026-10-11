/**
 * @jest-environment node
 */
import { loginAction } from '@/app/auth/login/actions';
import { signIn } from '@/auth';
import { BackendAuthError } from '@/lib/auth/credentials';

jest.mock('@/auth', () => ({ signIn: jest.fn() }));
const mockSignIn = signIn as jest.MockedFunction<typeof signIn>;

const body = { email: 'lan@example.com', password: 'Lan@2026x' };

beforeEach(() => mockSignIn.mockReset());

describe('loginAction — FR-AUTH-002', () => {
  it('FR-AUTH-002: tạo phiên qua provider "login", không redirect ở server', async () => {
    mockSignIn.mockResolvedValue(undefined);
    await expect(loginAction(body)).resolves.toEqual({ ok: true });
    expect(mockSignIn).toHaveBeenCalledWith('login', { ...body, redirect: false });
  });

  it('FR-AUTH-002/A1: 401 từ backend → trả ProblemDetails, không ném', async () => {
    const problem = { type: 'AUTH_INVALID_CREDENTIALS', title: 'Sai thông tin', status: 401 };
    mockSignIn.mockRejectedValue(new BackendAuthError(problem));
    await expect(loginAction(body)).resolves.toEqual({ ok: false, problem });
  });

  it('FR-AUTH-002: lỗi không phải của backend (cấu hình Auth.js…) → ném tiếp', async () => {
    mockSignIn.mockRejectedValue(new Error('MissingSecret'));
    await expect(loginAction(body)).rejects.toThrow('MissingSecret');
  });
});
