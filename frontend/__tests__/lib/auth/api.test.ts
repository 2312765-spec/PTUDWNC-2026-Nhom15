import { apiClient } from '@/lib/api-client';
import { getMe, googleLogin, login, logout, refresh, register } from '@/lib/auth/api';

describe('lib/auth/api — FR-AUTH-001', () => {
  it('FR-AUTH-001/D24: POST /auth/register với body D5 và trả nguyên AuthResponseDto', async () => {
    const body = { email: 'lan@example.com', password: 'Lan@2026x', displayName: 'Bếp của Lan' };
    const data = {
      accessToken: 'a',
      refreshToken: 'r',
      expiresAt: '2026-09-23T10:15:00Z',
      user: {},
    };
    const post = jest.spyOn(apiClient, 'post').mockResolvedValue({ data });

    await expect(register(body)).resolves.toBe(data);
    expect(post).toHaveBeenCalledWith('/auth/register', body, undefined);
  });
});

describe('lib/auth/api — FR-AUTH-002', () => {
  it('FR-AUTH-002/D24: POST /auth/login với { email, password } và trả nguyên AuthResponseDto', async () => {
    const body = { email: 'lan@example.com', password: 'Lan@2026x' };
    const data = {
      accessToken: 'a',
      refreshToken: 'r',
      expiresAt: '2026-09-23T10:15:00Z',
      user: {},
    };
    const post = jest.spyOn(apiClient, 'post').mockResolvedValue({ data });

    await expect(login(body)).resolves.toBe(data);
    expect(post).toHaveBeenCalledWith('/auth/login', body, undefined);
  });
});

describe('lib/auth/api — FR-AUTH-003', () => {
  it('FR-AUTH-003/D9: POST /auth/google với { idToken } và trả nguyên AuthResponseDto', async () => {
    const body = { idToken: 'google-id-token' };
    const data = {
      accessToken: 'a',
      refreshToken: 'r',
      expiresAt: '2026-09-23T10:15:00Z',
      user: {},
    };
    const post = jest.spyOn(apiClient, 'post').mockResolvedValue({ data });

    await expect(googleLogin(body)).resolves.toBe(data);
    expect(post).toHaveBeenCalledWith('/auth/google', body, undefined);
  });
});

describe('lib/auth/api — FR-AUTH-004', () => {
  it('FR-AUTH-004/D24: POST /auth/refresh với { refreshToken } và trả nguyên AuthResponseDto', async () => {
    const body = { refreshToken: 'raw-refresh' };
    const data = {
      accessToken: 'a2',
      refreshToken: 'r2',
      expiresAt: '2026-09-23T10:30:00Z',
      user: {},
    };
    const post = jest.spyOn(apiClient, 'post').mockResolvedValue({ data });

    await expect(refresh(body)).resolves.toBe(data);
    expect(post).toHaveBeenCalledWith('/auth/refresh', body, undefined);
  });
});

describe('lib/auth/api — FR-AUTH-005', () => {
  it('FR-AUTH-005/D47: POST /auth/logout với { refreshToken } và header Bearer', async () => {
    const post = jest.spyOn(apiClient, 'post').mockResolvedValue({ data: undefined });

    await expect(logout({ refreshToken: 'rt' }, 'access-token')).resolves.toBeUndefined();

    expect(post).toHaveBeenCalledWith(
      '/auth/logout',
      { refreshToken: 'rt' },
      { headers: { Authorization: 'Bearer access-token' } },
    );
  });
});

describe('lib/auth/api — FR-AUTH-006', () => {
  it('FR-AUTH-006/D5: GET /auth/me và trả nguyên UserProfile', async () => {
    const data = { id: 'u1', email: 'lan@example.com', displayName: 'Lan', avatarUrl: null, bio: null, roles: ['Author'] };
    const get = jest.spyOn(apiClient, 'get').mockResolvedValue({ data });

    await expect(getMe()).resolves.toBe(data);
    expect(get).toHaveBeenCalledWith('/auth/me');
  });
});
