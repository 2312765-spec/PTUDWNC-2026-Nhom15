/**
 * @jest-environment node
 */
import { CredentialsSignin } from 'next-auth';
import { googleLogin, register } from '@/lib/auth/api';
import { authorizeGoogle, authorizeRegister, BackendAuthError } from '@/lib/auth/credentials';
import type { AuthResponse } from '@/lib/types';
import { networkError, problemError } from '../../utils/problem';

jest.mock('@/lib/auth/api', () => ({
  register: jest.fn(),
  googleLogin: jest.fn(),
}));
const mockRegister = register as jest.MockedFunction<typeof register>;
const mockGoogleLogin = googleLogin as jest.MockedFunction<typeof googleLogin>;

const authResponse: AuthResponse = {
  accessToken: 'access-jwt',
  refreshToken: 'raw-refresh',
  expiresAt: '2026-09-23T10:15:00Z',
  user: {
    id: 'u1',
    email: 'lan@example.com',
    displayName: 'Bếp của Lan',
    avatarUrl: null,
    bio: null,
    roles: ['Author'],
  },
};

const credentials = { email: 'lan@example.com', password: 'Lan@2026x', displayName: 'Bếp của Lan' };

beforeEach(() => {
  mockRegister.mockReset();
  mockGoogleLogin.mockReset();
});

describe('authorizeRegister — FR-AUTH-001', () => {
  it('FR-AUTH-001/D5: gửi đúng { email, password, displayName }, bỏ qua field thừa', async () => {
    mockRegister.mockResolvedValue(authResponse);
    await authorizeRegister({ ...credentials, callbackUrl: '/', fullName: 'x' });
    expect(mockRegister).toHaveBeenCalledWith(credentials);
  });

  it('FR-AUTH-001/D24: thành công → user Auth.js mang hồ sơ + token của backend', async () => {
    mockRegister.mockResolvedValue(authResponse);
    await expect(authorizeRegister(credentials)).resolves.toEqual({
      id: 'u1',
      email: 'lan@example.com',
      name: 'Bếp của Lan',
      image: null,
      profile: authResponse.user,
      accessToken: 'access-jwt',
      refreshToken: 'raw-refresh',
      expiresAt: '2026-09-23T10:15:00Z',
    });
  });

  it('FR-AUTH-001/D4: 409 → BackendAuthError (là CredentialsSignin) giữ nguyên ProblemDetails', async () => {
    const problem = { type: 'AUTH_EMAIL_EXISTS', title: 'Email đã tồn tại', status: 409 };
    mockRegister.mockRejectedValue(problemError(problem));

    const error = await authorizeRegister(credentials).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(BackendAuthError);
    expect(error).toBeInstanceOf(CredentialsSignin);
    expect((error as BackendAuthError).code).toBe('AUTH_EMAIL_EXISTS');
    expect((error as BackendAuthError).problem).toEqual(problem);
  });

  it('FR-AUTH-001/D4: 400 VALIDATION_ERROR → giữ nguyên errors theo field', async () => {
    const problem = {
      type: 'VALIDATION_ERROR',
      title: 'Dữ liệu không hợp lệ',
      status: 400,
      errors: { Password: ['Password phải có ít nhất 1 chữ thường.'] },
    };
    mockRegister.mockRejectedValue(problemError(problem));

    const error = (await authorizeRegister(credentials).catch(
      (e: unknown) => e,
    )) as BackendAuthError;
    expect(error.problem.errors).toEqual(problem.errors);
  });

  it('FR-AUTH-001: API không phản hồi → NETWORK_ERROR', async () => {
    mockRegister.mockRejectedValue(networkError());
    const error = (await authorizeRegister(credentials).catch(
      (e: unknown) => e,
    )) as BackendAuthError;
    expect(error.code).toBe('NETWORK_ERROR');
  });
});

describe('authorizeGoogle — FR-AUTH-003', () => {
  it('FR-AUTH-003/D9: gửi đúng { idToken }', async () => {
    mockGoogleLogin.mockResolvedValue(authResponse);
    await authorizeGoogle('google-id-token');
    expect(mockGoogleLogin).toHaveBeenCalledWith({ idToken: 'google-id-token' });
  });

  it('FR-AUTH-003/D24: thành công → user Auth.js mang hồ sơ + token của backend, giống hệt authorizeLogin', async () => {
    mockGoogleLogin.mockResolvedValue(authResponse);
    await expect(authorizeGoogle('google-id-token')).resolves.toEqual({
      id: 'u1',
      email: 'lan@example.com',
      name: 'Bếp của Lan',
      image: null,
      profile: authResponse.user,
      accessToken: 'access-jwt',
      refreshToken: 'raw-refresh',
      expiresAt: '2026-09-23T10:15:00Z',
    });
  });

  it('FR-AUTH-003/D33: lỗi từ backend (token invalid/Google unavailable) truyền nguyên ra ngoài — Auth.js OAuth không có kênh ProblemDetails như authorize()', async () => {
    const backendError = problemError({
      type: 'AUTH_GOOGLE_TOKEN_INVALID',
      title: 'Bad Request',
      status: 400,
    });
    mockGoogleLogin.mockRejectedValue(backendError);

    await expect(authorizeGoogle('bad-token')).rejects.toBe(backendError);
  });
});
