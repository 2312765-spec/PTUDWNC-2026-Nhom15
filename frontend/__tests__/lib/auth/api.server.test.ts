/**
 * @jest-environment node
 */
import { apiClient } from '@/lib/api-client';
import { register } from '@/lib/auth/api';

describe('lib/auth/api (server) — FR-AUTH-001', () => {
  const original = process.env.API_INTERNAL_URL;
  afterEach(() => {
    process.env.API_INTERNAL_URL = original;
    jest.restoreAllMocks();
  });

  it('FR-AUTH-001: chạy ở server thì dùng API_INTERNAL_URL (URL nội bộ trong Docker)', async () => {
    process.env.API_INTERNAL_URL = 'http://api:8080/api/v1';
    const post = jest.spyOn(apiClient, 'post').mockResolvedValue({ data: {} });
    const body = { email: 'lan@example.com', password: 'Lan@2026x', displayName: 'Bếp của Lan' };

    await register(body);
    expect(post).toHaveBeenCalledWith('/auth/register', body, { baseURL: 'http://api:8080/api/v1' });
  });
});
