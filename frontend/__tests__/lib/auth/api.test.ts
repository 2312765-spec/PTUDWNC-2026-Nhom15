import { apiClient } from '@/lib/api-client';
import { register } from '@/lib/auth/api';

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
