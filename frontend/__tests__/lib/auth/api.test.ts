import { apiClient } from '@/lib/api-client';
import { googleLogin, register } from '@/lib/auth/api';

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
