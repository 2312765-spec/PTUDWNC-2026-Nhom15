/**
 * @jest-environment node
 */
import { apiClient } from '@/lib/api-client';
import { login, logout, refresh, register } from '@/lib/auth/api';

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
    expect(post).toHaveBeenCalledWith('/auth/register', body, {
      baseURL: 'http://api:8080/api/v1',
    });
  });

  it('FR-AUTH-002: login() ở server cũng dùng API_INTERNAL_URL', async () => {
    process.env.API_INTERNAL_URL = 'http://api:8080/api/v1';
    const post = jest.spyOn(apiClient, 'post').mockResolvedValue({ data: {} });
    const body = { email: 'lan@example.com', password: 'Lan@2026x' };

    await login(body);
    expect(post).toHaveBeenCalledWith('/auth/login', body, { baseURL: 'http://api:8080/api/v1' });
  });

  it('FR-AUTH-004: refresh() ở server (callback jwt / proxy) cũng dùng API_INTERNAL_URL', async () => {
    process.env.API_INTERNAL_URL = 'http://api:8080/api/v1';
    const post = jest.spyOn(apiClient, 'post').mockResolvedValue({ data: {} });
    const body = { refreshToken: 'raw-refresh' };

    await refresh(body);
    expect(post).toHaveBeenCalledWith('/auth/refresh', body, { baseURL: 'http://api:8080/api/v1' });
  });

  it('NFR-SEC-003/D62: có IP người dùng → gửi kèm X-Forwarded-For (API rate limit theo người dùng)', async () => {
    process.env.API_INTERNAL_URL = 'http://api:8080/api/v1';
    const post = jest.spyOn(apiClient, 'post').mockResolvedValue({ data: {} });
    const body = { email: 'lan@example.com', password: 'Lan@2026x' };

    await login(body, '203.0.113.7');
    expect(post).toHaveBeenCalledWith('/auth/login', body, {
      baseURL: 'http://api:8080/api/v1',
      headers: { 'X-Forwarded-For': '203.0.113.7' },
    });
  });

  it('FR-AUTH-005/D62: logout() giữ Authorization khi gửi thêm X-Forwarded-For', async () => {
    process.env.API_INTERNAL_URL = 'http://api:8080/api/v1';
    const post = jest.spyOn(apiClient, 'post').mockResolvedValue({ data: undefined });
    const body = { refreshToken: 'raw-refresh' };

    await logout(body, 'access-jwt', '203.0.113.7');
    expect(post).toHaveBeenCalledWith('/auth/logout', body, {
      baseURL: 'http://api:8080/api/v1',
      headers: { 'X-Forwarded-For': '203.0.113.7', Authorization: 'Bearer access-jwt' },
    });
  });
});
