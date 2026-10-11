/**
 * @jest-environment node
 */
import { registerAction } from '@/app/auth/register/actions';
import { signIn } from '@/auth';
import { BackendAuthError } from '@/lib/auth/credentials';

jest.mock('@/auth', () => ({ signIn: jest.fn() }));
const mockSignIn = signIn as jest.MockedFunction<typeof signIn>;

const body = { email: 'lan@example.com', password: 'Lan@2026x', displayName: 'Bếp của Lan' };

beforeEach(() => mockSignIn.mockReset());

describe('registerAction — FR-AUTH-001', () => {
  it('FR-AUTH-001/D24: đăng ký + tạo phiên qua provider "register", không redirect ở server', async () => {
    mockSignIn.mockResolvedValue(undefined);
    await expect(registerAction(body)).resolves.toEqual({ ok: true });
    expect(mockSignIn).toHaveBeenCalledWith('register', { ...body, redirect: false });
  });

  it('FR-AUTH-001/D4: lỗi backend → trả ProblemDetails, không ném', async () => {
    const problem = { type: 'AUTH_EMAIL_EXISTS', title: 'Email đã tồn tại', status: 409 };
    mockSignIn.mockRejectedValue(new BackendAuthError(problem));
    await expect(registerAction(body)).resolves.toEqual({ ok: false, problem });
  });

  it('FR-AUTH-001: lỗi không phải của backend (cấu hình Auth.js…) → ném tiếp', async () => {
    mockSignIn.mockRejectedValue(new Error('MissingSecret'));
    await expect(registerAction(body)).rejects.toThrow('MissingSecret');
  });
});
